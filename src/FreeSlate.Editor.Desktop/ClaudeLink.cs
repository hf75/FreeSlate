using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;
using FreeSlate.Editor.Asking;

namespace FreeSlate.Editor.Desktop;

/// <summary>
/// Claude reached through Anthropic's API with its SDK, by the key in ANTHROPIC_API_KEY (the user's own setting where the process was
/// started without it): Claude Opus 5.5, thinking as far as it sees fit, its answers streamed, the inputs of tools that say so streaming
/// as they are written; the instructions and the conversation cached; a request the model declines taken on by the model Anthropic
/// recommends for it (server-side fallback).
/// </summary>
sealed class ClaudeLink(AnthropicClient client) : IClaudeLink
{
    const string Model = "claude-opus-5-5";

    /// <summary>What each turn used, as the API counts it (tokens in, of them read from and written to the cache, and out): for those who watch.</summary>
    public static event Action<string>? Used;

    /// <summary>The link where a key is set, else null (asking then says Claude needs one).</summary>
    public static ClaudeLink? FromEnvironment()
    {
        var key = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(key) && OperatingSystem.IsWindows())
            key = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY", EnvironmentVariableTarget.User);
        return string.IsNullOrWhiteSpace(key) ? null : new ClaudeLink(new AnthropicClient { ApiKey = key });
    }

    public IClaudeConversation Begin(string system, IReadOnlyList<ClaudeTool> tools) => new Conversation(client, system, tools);

    /// <summary>A block of Claude's answer as it streams in, to be given back as it was in the next turn.</summary>
    sealed class Block(string kind)
    {
        public string Kind { get; } = kind;
        public StringBuilder Text { get; } = new();
        public string Signature { get; set; } = "";
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public BetaFallbackBlock? Fallback { get; set; }
    }

    sealed class Conversation(AnthropicClient client, string system, IReadOnlyList<ClaudeTool> tools) : IClaudeConversation
    {
        readonly List<BetaMessageParam> messages = [];
        readonly List<BetaToolUnion> defined = [.. tools.Select(Define)];

        static BetaToolUnion Define(ClaudeTool tool)
        {
            using var schema = JsonDocument.Parse(tool.Schema);
            var root = schema.RootElement;
            return new BetaTool
            {
                Name = tool.Name,
                Description = tool.Description,
                InputSchema = new()
                {
                    Properties = root.GetProperty("properties").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone()),
                    Required = [.. root.GetProperty("required").EnumerateArray().Select(r => r.GetString()!)],
                },
                EagerInputStreaming = tool.Streams ? true : null,
            };
        }

        static BetaContentBlockParam Param(ClaudePart part) => part switch
        {
            TextPart text => new BetaTextBlockParam { Text = text.Text },
            ImagePart image => Image(image),
            ToolResultPart result => new BetaToolResultBlockParam
            {
                ToolUseID = result.Id,
                IsError = result.Error ? true : null,
                Content = result.Images.Count == 0 ? result.Text : Shown(result),
            },
            _ => throw new ArgumentException("A part of a kind the link does not know.", nameof(part)),
        };

        /// <summary>What a tool gave with pictures: its text, then the pictures.</summary>
        static List<Anthropic.Models.Beta.Messages.Block> Shown(ToolResultPart result)
        {
            List<Anthropic.Models.Beta.Messages.Block> shown = [new BetaTextBlockParam { Text = result.Text }];
            foreach (var image in result.Images)
                shown.Add(Image(image));
            return shown;
        }

        static BetaImageBlockParam Image(ImagePart image) => new()
        {
            Source = new BetaBase64ImageSource { Data = Convert.ToBase64String(image.Bytes), MediaType = image.MediaType },
        };

        public async IAsyncEnumerable<ClaudeEvent> Turn(IReadOnlyList<ClaudePart> parts, [EnumeratorCancellation] CancellationToken cancel)
        {
            messages.Add(new BetaUserMessageParam { Content = parts.Select(Param).ToList() });
            var parameters = new MessageCreateParams
            {
                Model = Model,
                MaxTokens = 64000,
                System = new List<BetaTextBlockParam> { new() { Text = system, CacheControl = new BetaCacheControlEphemeral() } },
                Tools = defined,
                Messages = [.. messages],
                Thinking = new BetaThinkingConfigAdaptive(),
                OutputConfig = new BetaOutputConfig { Effort = Effort.Medium },
                CacheControl = new BetaCacheControlEphemeral(),
                Betas = [AnthropicBeta.ServerSideFallback2026_07_01],
                Fallbacks = new Default(),
            };
            var blocks = new SortedDictionary<long, Block>();
            var stop = "end_turn";
            string? explanation = null;
            var used = "";
            await foreach (var streamed in client.Beta.Messages.CreateStreaming(parameters, cancel))
            {
                if (streamed.TryPickStart(out var start))
                    used = $"in {start.Message.Usage.InputTokens}, cache read {start.Message.Usage.CacheReadInputTokens}, cache written {start.Message.Usage.CacheCreationInputTokens}";
                if (streamed.TryPickContentBlockStart(out var started))
                {
                    var content = started.ContentBlock;
                    if (content.TryPickBetaText(out var text))
                        (blocks[started.Index] = new Block("text")).Text.Append(text.Text);
                    else if (content.TryPickBetaThinking(out var thinking))
                    {
                        var block = blocks[started.Index] = new Block("thinking") { Signature = thinking.Signature };
                        block.Text.Append(thinking.Thinking);
                        yield return new Asking.Thinking();
                    }
                    else if (content.TryPickBetaRedactedThinking(out var redacted))
                        (blocks[started.Index] = new Block("redacted")).Text.Append(redacted.Data);
                    else if (content.TryPickBetaToolUse(out var use))
                    {
                        blocks[started.Index] = new Block("tool") { Id = use.ID, Name = use.Name };
                        yield return new ToolBegan(use.ID, use.Name);
                    }
                    else if (content.TryPickBetaFallback(out var fallback))
                    {
                        blocks[started.Index] = new Block("fallback") { Fallback = fallback };
                        yield return new FellBack();
                    }
                }
                else if (streamed.TryPickContentBlockDelta(out var delta) && blocks.TryGetValue(delta.Index, out var growing))
                {
                    if (delta.Delta.TryPickText(out var text))
                    {
                        growing.Text.Append(text.Text);
                        yield return new TextGrew(text.Text);
                    }
                    else if (delta.Delta.TryPickInputJson(out var json))
                    {
                        growing.Text.Append(json.PartialJson);
                        yield return new ToolGrew(growing.Id, growing.Text.ToString());
                    }
                    else if (delta.Delta.TryPickThinking(out var thinking))
                        growing.Text.Append(thinking.Thinking);
                    else if (delta.Delta.TryPickSignature(out var signature))
                        growing.Signature = signature.Signature;
                }
                else if (streamed.TryPickContentBlockStop(out var stopped) && blocks.TryGetValue(stopped.Index, out var whole) && whole.Kind == "tool")
                    yield return new ToolCalled(whole.Id, whole.Name, whole.Text.Length > 0 ? whole.Text.ToString() : "{}");
                else if (streamed.TryPickDelta(out var ending))
                {
                    if (ending.Delta.StopReason is { } reason)
                        stop = reason.Raw();
                    if (ending.Delta.StopDetails is { } details)
                        explanation = details.Explanation;
                    used += $", out {ending.Usage.OutputTokens}";
                }
            }
            Used?.Invoke($"{used}, stop {stop}");
            messages.Add(new BetaAssistantMessageParam { Content = Given(blocks.Values.ToList()) });
            yield return new TurnEnded(stop, explanation);
        }

        /// <summary>
        /// Claude's answer as it is given back: each block as it came, thinking with its signature; before the last point where another
        /// model took over only the text, and that point itself, as the API reads it on.
        /// </summary>
        static List<BetaContentBlockParam> Given(List<Block> blocks)
        {
            var last = blocks.FindLastIndex(b => b.Kind == "fallback");
            var given = new List<BetaContentBlockParam>();
            for (var k = 0; k < blocks.Count; k++)
            {
                var block = blocks[k];
                if (k < last && block.Kind != "text")
                    continue;
                switch (block.Kind)
                {
                    case "text" when block.Text.Length > 0:
                        given.Add(new BetaTextBlockParam { Text = block.Text.ToString() });
                        break;
                    case "thinking":
                        given.Add(new BetaThinkingBlockParam { Thinking = block.Text.ToString(), Signature = block.Signature });
                        break;
                    case "redacted":
                        given.Add(new BetaRedactedThinkingBlockParam { Data = block.Text.ToString() });
                        break;
                    case "tool":
                        given.Add(new BetaToolUseBlockParam { ID = block.Id, Name = block.Name, Input = Input(block.Text.ToString()) });
                        break;
                    case "fallback" when block.Fallback is { } fallback:
                        given.Add(new BetaFallbackBlockParam { From = new BetaFallbackInfoParam { Model = fallback.From.Model }, To = new BetaFallbackInfoParam { Model = fallback.To.Model } });
                        break;
                }
            }
            return given;
        }

        /// <summary>A tool's input as it is given back: its members, or none where it could not be read.</summary>
        static IReadOnlyDictionary<string, JsonElement> Input(string json)
        {
            try
            {
                using var input = JsonDocument.Parse(json.Length > 0 ? json : "{}");
                return input.RootElement.ValueKind == JsonValueKind.Object
                    ? input.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone())
                    : new Dictionary<string, JsonElement>();
            }
            catch (JsonException)
            {
                return new Dictionary<string, JsonElement>();
            }
        }
    }
}

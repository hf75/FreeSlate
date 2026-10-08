using System.Diagnostics;
using System.Runtime.Versioning;

namespace Fidelity.PowerPoint;

/// <summary>
/// Audio and video (catalog: picture.media): a synthetic test pattern video that plays automatically, trimmed
/// and faded, and a sine tone that plays on click. The media are generated with ffmpeg, so the deck contains
/// only synthetic content.
/// </summary>
[SupportedOSPlatform("windows")]
static class MediaDeck
{
    const int MsoTrue = PowerPointSession.MsoTrue, MsoFalse = PowerPointSession.MsoFalse;

    public static void Build(dynamic deck)
    {
        var directory = Path.Combine(Path.GetTempPath(), "fidelity-media");
        Directory.CreateDirectory(directory);
        var video = Path.Combine(directory, "pattern.mp4");
        var audio = Path.Combine(directory, "tone.wav");
        Ffmpeg($"-y -f lavfi -i testsrc=duration=4:size=320x180:rate=15 -pix_fmt yuv420p -c:v libx264 -profile:v baseline -movflags +faststart \"{video}\"");
        Ffmpeg($"-y -f lavfi -i sine=frequency=440:duration=2 -ac 1 -ar 22050 \"{audio}\"");

        object presentation = deck;
        var videoSlide = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutTitleOnly);
        videoSlide.Shapes.Placeholders[1].TextFrame.TextRange.Text = "Video";
        var movie = videoSlide.Shapes.AddMediaObject2(video, MsoFalse, MsoTrue, 160, 130, 640, 360);
        movie.AnimationSettings.PlaySettings.PlayOnEntry = MsoTrue;
        movie.MediaFormat.StartPoint = 500;
        movie.MediaFormat.EndPoint = 3500;
        movie.MediaFormat.FadeInDuration = 300;

        var audioSlide = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutTitleOnly);
        audioSlide.Shapes.Placeholders[1].TextFrame.TextRange.Text = "Audio";
        var sound = audioSlide.Shapes.AddMediaObject2(audio, MsoFalse, MsoTrue, 420, 220, 64, 64);
        sound.MediaFormat.Volume = 0.5f;
    }

    static void Ffmpeg(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("ffmpeg", arguments)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("ffmpeg is needed to generate the media deck.");
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg failed: {arguments}");
    }
}

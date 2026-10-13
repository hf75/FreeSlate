using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace FreeSlate.Viewer;

/// <summary>
/// Serves the document and Word's pages of it on this computer only (http://localhost, a free port), so that the
/// pictures of a comparison come from the document's own origin.
/// </summary>
sealed class DocumentServer : IDisposable
{
    readonly HttpListener listener = new();
    readonly Uri address;
    readonly ConcurrentDictionary<string, string> folders = new();
    string? document;
    string? route;

    public DocumentServer()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        address = new Uri($"http://localhost:{port}/");
        listener.Prefixes.Add(address.AbsoluteUri);
        listener.Start();
        _ = Serve();
    }

    /// <summary>The address of a document from now on (the previous one is no longer served).</summary>
    public Uri Publish(string path)
    {
        document = path;
        route = "/" + Guid.NewGuid().ToString("N") + "/" + Uri.EscapeDataString(Path.GetFileName(path));
        return new Uri(address, route);
    }

    /// <summary>The address of a folder's files, each by its name (nothing in folders below it).</summary>
    public Uri PublishFolder(string folder)
    {
        var prefix = "/" + Guid.NewGuid().ToString("N") + "/";
        folders[prefix] = folder;
        return new Uri(address, prefix);
    }

    /// <summary>Whether an address is this server's.</summary>
    public bool Serves(Uri uri) => uri.IsAbsoluteUri && uri.IsLoopback && uri.Port == address.Port;

    async Task Serve()
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try { context = await listener.GetContextAsync(); }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException) { return; }
            try
            {
                var response = context.Response;
                if (Find(context.Request.Url?.AbsolutePath) is { } file && File.Exists(file))
                {
                    var bytes = await File.ReadAllBytesAsync(file);
                    response.ContentType = Path.GetExtension(file).ToLowerInvariant() switch
                    {
                        ".html" => "text/html; charset=utf-8",
                        ".png" => "image/png",
                        ".pdf" => "application/pdf",
                        _ => "application/octet-stream",
                    };
                    response.Headers["Cache-Control"] = "no-store";
                    response.ContentLength64 = bytes.Length;
                    await response.OutputStream.WriteAsync(bytes);
                }
                else
                {
                    response.StatusCode = 404;
                }
                response.Close();
            }
            catch (Exception e) when (e is HttpListenerException or IOException)
            {
                // The web view went away mid-request.
            }
        }
    }

    string? Find(string? path)
    {
        if (path == null)
            return null;
        if (document != null && path == route)
            return document;
        var slash = path.IndexOf('/', 1);
        if (slash < 0 || !folders.TryGetValue(path[..(slash + 1)], out var folder))
            return null;
        var name = Uri.UnescapeDataString(path[(slash + 1)..]);
        return name.Length > 0 && name.IndexOfAny(['/', '\\']) < 0 && name is not ("." or "..") ? Path.Combine(folder, name) : null;
    }

    public void Dispose() => listener.Close();
}

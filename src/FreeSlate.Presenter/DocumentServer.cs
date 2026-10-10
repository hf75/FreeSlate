using System.Net;
using System.Net.Sockets;

namespace FreeSlate.Presenter;

/// <summary>
/// Serves the presentation's document on this computer only (http://localhost, a free port): a page from a file has
/// no origin to tell embedded web players such as YouTube's, which then refuse to play (error 153).
/// </summary>
sealed class DocumentServer : IDisposable
{
    readonly HttpListener listener = new();
    readonly Uri address;
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
                if (document != null && context.Request.Url?.AbsolutePath == route && File.Exists(document))
                {
                    var bytes = await File.ReadAllBytesAsync(document);
                    response.ContentType = "text/html; charset=utf-8";
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

    public void Dispose() => listener.Close();
}

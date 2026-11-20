using System.Text;
using System.Text.Json;
using Deslocai.Security.Remediation;

namespace Deslocai.Security.Taint;


public class TaintMiddleware
{
    private static readonly HashSet<string> IgnoredHeaders = new(StringComparer.OrdinalIgnoreCase)
    { "Host", "User-Agent", "Accept", "Accept-Encoding", "Accept-Language", "Connection", "Content-Type", "Content-Length", "Origin", "Referer", "Cache-Control" };

    private readonly RequestDelegate _next;
    public TaintMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext http, TaintContext ctx, SinkGuard guard, CitizenTranslator translator)
    {
        foreach (var q in http.Request.Query) foreach (var v in q.Value) ctx.AddSource($"query.{q.Key}", v);
        foreach (var h in http.Request.Headers.Where(h => !IgnoredHeaders.Contains(h.Key) && !h.Key.StartsWith("sec-", StringComparison.OrdinalIgnoreCase)))
            foreach (var v in h.Value) ctx.AddSource($"header.{h.Key}", v);

        if (http.Request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true
            && http.Request.ContentLength is > 0 and <= 64 * 1024)
        {
            http.Request.EnableBuffering();
            try
            {
                using var doc = await JsonDocument.ParseAsync(http.Request.Body, default, http.RequestAborted);
                Walk(doc.RootElement, "body", ctx);
            }
            catch (JsonException) {  }
            http.Request.Body.Position = 0;
        }

        if (ctx.Entries.Count == 0) { await _next(http); return; }   

        var original = http.Response.Body;
        using var buffer = new MemoryStream();
        http.Response.Body = buffer;
        try
        {
            try { await _next(http); }
            catch (TaintBlockedException ex)
            {
                buffer.SetLength(0);
                var (msg, source) = await translator.TranslateAsync(ex.Finding, http.RequestAborted);
                http.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                http.Response.ContentType = "application/json; charset=utf-8";
                var json = JsonSerializer.Serialize(new
                {
                    codigo = "TAINT_BLOCK", titulo = "Bloqueamos uma ação para proteger os seus dados",
                    mensagem = msg, farol = "amarelo", origemMensagem = source,
                });
                await buffer.WriteAsync(Encoding.UTF8.GetBytes(json), http.RequestAborted);
            }

            var ct = http.Response.ContentType ?? "";
            buffer.Position = 0;
            byte[] output;
            if (ct.Contains("json", StringComparison.OrdinalIgnoreCase) || ct.Contains("text", StringComparison.OrdinalIgnoreCase))
            {
                var text = await new StreamReader(buffer, Encoding.UTF8, leaveOpen: true).ReadToEndAsync();
                output = Encoding.UTF8.GetBytes(guard.Response(text));   
            }
            else output = buffer.ToArray();

            http.Response.Body = original;
            http.Response.ContentLength = output.Length;
            await original.WriteAsync(output, http.RequestAborted);
        }
        finally { http.Response.Body = original; }
    }

    private static void Walk(JsonElement el, string path, TaintContext ctx)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object: foreach (var p in el.EnumerateObject()) Walk(p.Value, $"{path}.{p.Name}", ctx); break;
            case JsonValueKind.Array: int i = 0; foreach (var item in el.EnumerateArray()) Walk(item, $"{path}[{i++}]", ctx); break;
            case JsonValueKind.String: ctx.AddSource(path, el.GetString()); break;
            case JsonValueKind.Number: ctx.AddSource(path, el.GetRawText()); break;
        }
    }
}

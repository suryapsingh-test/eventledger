using System.Net;
using System.Net.Http.Json;

namespace EventLedger.IntegrationTests.Infrastructure;

/// <summary>
/// Intercepts outbound Account Service HTTP calls for resiliency and trace assertions.
/// </summary>
public sealed class CapturingAccountServiceHandler : HttpMessageHandler
{
    private readonly object _sync = new();
    private HttpStatusCode _responseStatusCode = HttpStatusCode.Created;

    public IReadOnlyList<HttpRequestMessage> Requests
    {
        get
        {
            lock (_sync)
            {
                return RequestsInternal.ToList();
            }
        }
    }

    private List<HttpRequestMessage> RequestsInternal { get; } = [];

    public int RequestCount
    {
        get
        {
            lock (_sync)
            {
                return RequestsInternal.Count;
            }
        }
    }

    public HttpStatusCode ResponseStatusCode
    {
        get
        {
            lock (_sync)
            {
                return _responseStatusCode;
            }
        }
        set
        {
            lock (_sync)
            {
                _responseStatusCode = value;
            }
        }
    }

    /// <summary>When &gt; 0, returns <see cref="TransientFailureStatusCode"/> and decrements until zero.</summary>
    public int RemainingTransientFailures { get; set; }

    public HttpStatusCode TransientFailureStatusCode { get; set; } = HttpStatusCode.ServiceUnavailable;

    public string? LastTraceParent
    {
        get
        {
            lock (_sync)
            {
                var last = RequestsInternal.LastOrDefault();
                if (last?.Headers.TryGetValues("traceparent", out var values) == true)
                    return values.FirstOrDefault();

                return null;
            }
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            foreach (var request in RequestsInternal)
            {
                request.Dispose();
            }

            RequestsInternal.Clear();
        }

        RemainingTransientFailures = 0;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            RequestsInternal.Add(request);
        }

        var statusCode = ResponseStatusCode;

        lock (_sync)
        {
            if (RemainingTransientFailures > 0)
            {
                RemainingTransientFailures--;
                statusCode = TransientFailureStatusCode;
            }
        }

        var response = new HttpResponseMessage(statusCode);

        if (statusCode is HttpStatusCode.OK or HttpStatusCode.Created)
        {
            response.Content = JsonContent.Create(new
            {
                accountId = "integration-test",
                balance = 100m,
                currency = "USD"
            });
        }

        return Task.FromResult(response);
    }
}

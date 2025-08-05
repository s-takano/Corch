using System.Collections;
using System.Collections.Immutable;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace CorchEdges.Tests.Infrastructure;

public class TestHttpRequestData : HttpRequestData
{
    private readonly string? _body;
    private readonly Uri _url;
    private readonly TestFunctionContext _functionContext;
    
    public TestHttpRequestData(TestFunctionContext functionContext, string url, string? body = null) 
        : base(functionContext)
    {
        _functionContext = functionContext;
        Identities = new List<ClaimsIdentity>();
        _url = new Uri(url);
        _body = body;
        Headers = new HttpHeadersCollection();
        Cookies = new List<IHttpCookie>();
    }

    public override Stream Body => new MemoryStream(Encoding.UTF8.GetBytes(_body ?? ""));
    public override HttpHeadersCollection Headers { get; }
    public override IReadOnlyCollection<IHttpCookie> Cookies { get; }
    public override Uri Url => _url;
    public override IEnumerable<ClaimsIdentity> Identities { get; }
    public override string Method { get; } = "POST";

    public override HttpResponseData CreateResponse()
    {
        return new TestHttpResponseData(_functionContext);
    }
}

public class TestHttpResponseData : HttpResponseData
{
    private MemoryStream _bodyStream = new();
    
    public TestHttpResponseData(FunctionContext functionContext) : base(functionContext)
    {
        Headers = new HttpHeadersCollection();
        StatusCode = HttpStatusCode.OK;
        Cookies = new TestHttpCookies();
    }

    public override HttpStatusCode StatusCode { get; set; }
    public override HttpHeadersCollection Headers { get; set; }
    public override Stream Body
    {
        get => _bodyStream;
        set => _bodyStream = value as MemoryStream ?? throw new ArgumentException("Body must be a MemoryStream");
    }

    public override HttpCookies Cookies { get; }

    // Helper to get written content
    public string GetWrittenContent()
    {
        _bodyStream.Position = 0;
        using var reader = new StreamReader(_bodyStream);
        return reader.ReadToEnd();
    }
}

public class TestHttpCookies : HttpCookies
{
    private readonly List<IHttpCookie> _cookies = new();
    
    public override void Append(string name, string value) 
    {
        _cookies.Add(new TestHttpCookie(name, value));
    }
    
    public override void Append(IHttpCookie cookie) 
    {
        _cookies.Add(cookie);
    }
    
    public override IHttpCookie CreateNew() 
    {
        return new TestHttpCookie("", "");
    }
}

public class TestHttpCookie : IHttpCookie
{
    public TestHttpCookie(string name, string value)
    {
        Name = name;
        Value = value;
    }
    
    public string Name { get; set; }
    public string Value { get; set; }
    public string? Domain { get; set; }
    public string? Path { get; set; }
    public DateTimeOffset? Expires { get; set; }
    public bool? HttpOnly { get; set; }
    public bool? Secure { get; set; }
    public SameSite SameSite { get; set; }
    public double? MaxAge { get; set; }
}

public class TestFunctionContext : FunctionContext
{
    public override string InvocationId { get; } = Guid.NewGuid().ToString();
    public override string FunctionId { get; } = "test-function";
    public override TraceContext TraceContext { get; } = new TestTraceContext();
    public override BindingContext BindingContext { get; } = new TestBindingContext();
    public override RetryContext RetryContext { get; } = new TestRetryContext();
    public override IServiceProvider InstanceServices { get; set; } = new TestServiceProvider();
    public override FunctionDefinition FunctionDefinition { get; } = new TestFunctionDefinition();
    public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();
    public override IInvocationFeatures Features { get; } = new TestInvocationFeatures();
}

// Minimal implementations for the required context classes
public class TestTraceContext : TraceContext
{
    public override string TraceParent => "00-test-test-00";
    public override string TraceState => "";
}

public class TestBindingContext : BindingContext
{
    public override IReadOnlyDictionary<string, object?> BindingData => new Dictionary<string, object?>();
}

public class TestRetryContext : RetryContext
{
    public override int RetryCount => 0;
    public override int MaxRetryCount => 0;
}

public class TestServiceProvider : IServiceProvider
{
    public object? GetService(Type serviceType) => null;
}

public class TestFunctionDefinition : FunctionDefinition
{
    public override string PathToAssembly => "";
    public override string EntryPoint => "";
    public override string Id => "test-function";
    public override string Name => "TestFunction";
    public override ImmutableArray<FunctionParameter> Parameters => ImmutableArray<FunctionParameter>.Empty;
    
    // Fixed: Use ImmutableDictionary instead of Dictionary
    public override IImmutableDictionary<string, BindingMetadata> InputBindings => 
        ImmutableDictionary<string, BindingMetadata>.Empty;
    
    public override IImmutableDictionary<string, BindingMetadata> OutputBindings => 
        ImmutableDictionary<string, BindingMetadata>.Empty;
}

public class TestInvocationFeatures : IInvocationFeatures
{
    private readonly Dictionary<Type, object> _features = new();
    
    public T? Get<T>() => _features.TryGetValue(typeof(T), out var feature) ? (T)feature : default;
    public void Set<T>(T? instance) 
    {
        if (instance != null)
            _features[typeof(T)] = instance;
        else
            _features.Remove(typeof(T));
    }

    public IEnumerator<KeyValuePair<Type, object>> GetEnumerator()
    {
        return _features.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
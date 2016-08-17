// Decompiled with JetBrains decompiler
// Type: System.Net.HttpWebRequest
// Assembly: System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089
// MVID: C251657A-8928-4869-9043-AD3913D9E471
// Assembly location: C:\Windows\Microsoft.NET\Framework\v4.0.30319\System.dll

using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.IO;
using System.Net.Cache;
using System.Net.Configuration;
using System.Net.Security;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Permissions;
using System.Text;
using System.Threading;

namespace System.Net
{
  /// <summary>
  /// Provides an HTTP-specific implementation of the <see cref="T:System.Net.WebRequest"/> class.
  /// </summary>
  [FriendAccessAllowed]
  [__DynamicallyInvokable]
  [Serializable]
  public class HttpWebRequest : WebRequest, ISerializable
  {
    private static readonly byte[] HttpBytes = new byte[5]
    {
      (byte) 72,
      (byte) 84,
      (byte) 84,
      (byte) 80,
      (byte) 47
    };
    private static readonly WaitCallback s_EndWriteHeaders_Part2Callback = new WaitCallback(HttpWebRequest.EndWriteHeaders_Part2Wrapper);
    private static readonly TimerThread.Callback s_ContinueTimeoutCallback = new TimerThread.Callback(HttpWebRequest.ContinueTimeoutCallback);
    private static readonly TimerThread.Queue s_ContinueTimerQueue = TimerThread.GetOrCreateQueue(350);
    private static readonly TimerThread.Callback s_TimeoutCallback = new TimerThread.Callback(HttpWebRequest.TimeoutCallback);
    private static readonly WaitCallback s_AbortWrapper = new WaitCallback(HttpWebRequest.AbortWrapper);
    private static System.PinnableBufferCache _WriteBufferCache = new System.PinnableBufferCache("System.Net.HttpWebRequest", 512);
    private bool m_KeepAlive = true;
    private bool m_Pipelined = true;
    private bool m_Retry = true;
    private bool m_NeedsToReadForResponse = true;
    private HttpWebRequest.Booleans _Booleans = HttpWebRequest.Booleans.Default;
    private bool m_Saw100Continue;
    private bool m_LockConnection;
    private bool m_NtlmKeepAlive;
    private bool m_PreAuthenticate;
    private DecompressionMethods m_AutomaticDecompression;
    private int m_Aborted;
    private bool m_OnceFailed;
    private bool m_HeadersCompleted;
    private bool m_IsCurrentAuthenticationStateProxy;
    private bool m_BodyStarted;
    private bool m_RequestSubmitted;
    private bool m_OriginallyBuffered;
    private bool m_Extra401Retry;
    private long m_StartTimestamp;
    internal const HttpStatusCode MaxOkStatus = (HttpStatusCode) 299;
    private const HttpStatusCode MaxRedirectionStatus = (HttpStatusCode) 399;
    private const int RequestLineConstantSize = 12;
    private const string ContinueHeader = "100-continue";
    internal const string ChunkedHeader = "chunked";
    internal const string GZipHeader = "gzip";
    internal const string DeflateHeader = "deflate";
    internal const int DefaultReadWriteTimeout = 300000;
    internal const int DefaultContinueTimeout = 350;
    private static int s_UniqueGroupId;
    private TimerThread.Timer m_ContinueTimer;
    private InterlockedGate m_ContinueGate;
    private int m_ContinueTimeout;
    private TimerThread.Queue m_ContinueTimerQueue;
    private object m_PendingReturnResult;
    private LazyAsyncResult _WriteAResult;
    private LazyAsyncResult _ReadAResult;
    private LazyAsyncResult _ConnectionAResult;
    private LazyAsyncResult _ConnectionReaderAResult;
    private TriState _RequestIsAsync;
    private HttpContinueDelegate _ContinueDelegate;
    internal ServicePoint _ServicePoint;
    internal HttpWebResponse _HttpResponse;
    private object _CoreResponse;
    private int _NestedWriteSideCheck;
    private KnownHttpVerb _Verb;
    private KnownHttpVerb _OriginVerb;
    private bool _HostHasPort;
    private Uri _HostUri;
    private WebHeaderCollection _HttpRequestHeaders;
    private byte[] _WriteBuffer;
    private int _WriteBufferLength;
    private const int CachedWriteBufferSize = 512;
    private bool _WriteBufferFromPinnableCache;
    private HttpWriteMode _HttpWriteMode;
    private Uri _Uri;
    private Uri _OriginUri;
    private string _MediaType;
    private long _ContentLength;
    private IWebProxy _Proxy;
    private ProxyChain _ProxyChain;
    private string _ConnectionGroupName;
    private bool m_InternalConnectionGroup;
    private AuthenticationState _ProxyAuthenticationState;
    private AuthenticationState _ServerAuthenticationState;
    private ICredentials _AuthInfo;
    private HttpAbortDelegate _AbortDelegate;
    private ConnectStream _SubmitWriteStream;
    private ConnectStream _OldSubmitWriteStream;
    private int _MaximumAllowedRedirections;
    private int _AutoRedirects;
    private bool _RedirectedToDifferentHost;
    private int _RerequestCount;
    private int _Timeout;
    private TimerThread.Timer _Timer;
    private TimerThread.Queue _TimerQueue;
    private int _RequestContinueCount;
    private int _ReadWriteTimeout;
    private CookieContainer _CookieContainer;
    private int _MaximumResponseHeadersLength;
    private UnlockConnectionDelegate _UnlockDelegate;
    private bool _returnResponseOnFailureStatusCode;
    private Action<Stream> _resendRequestContent;
    private long _originalContentLength;
    private X509CertificateCollection _ClientCertificates;

    [FriendAccessAllowed]
    internal RtcState RtcState { get; set; }

    internal TimerThread.Timer RequestTimer
    {
      get
      {
        return this._Timer;
      }
    }

    internal bool Aborted
    {
      get
      {
        return (uint) this.m_Aborted > 0U;
      }
    }

    /// <summary>
    /// Gets or sets a value that indicates whether the request should follow redirection responses.
    /// </summary>
    /// 
    /// <returns>
    /// true if the request should automatically follow redirection responses from the Internet resource; otherwise, false. The default value is true.
    /// </returns>
    public virtual bool AllowAutoRedirect
    {
      get
      {
        return (this._Booleans & HttpWebRequest.Booleans.AllowAutoRedirect) > (HttpWebRequest.Booleans) 0;
      }
      set
      {
        if (value)
          this._Booleans = this._Booleans | HttpWebRequest.Booleans.AllowAutoRedirect;
        else
          this._Booleans = this._Booleans & ~HttpWebRequest.Booleans.AllowAutoRedirect;
      }
    }

    /// <summary>
    /// Gets or sets a value that indicates whether to buffer the data sent to the Internet resource.
    /// </summary>
    /// 
    /// <returns>
    /// true to enable buffering of the data sent to the Internet resource; false to disable buffering. The default is true.
    /// </returns>
    public virtual bool AllowWriteStreamBuffering
    {
      get
      {
        return (this._Booleans & HttpWebRequest.Booleans.AllowWriteStreamBuffering) > (HttpWebRequest.Booleans) 0;
      }
      set
      {
        if (value)
          this._Booleans = this._Booleans | HttpWebRequest.Booleans.AllowWriteStreamBuffering;
        else
          this._Booleans = this._Booleans & ~HttpWebRequest.Booleans.AllowWriteStreamBuffering;
      }
    }

    /// <summary>
    /// Gets or sets a value that indicates whether to buffer the received from the Internet resource.
    /// </summary>
    /// 
    /// <returns>
    /// true to buffer the received from the Internet resource; otherwise, false.true to enable buffering of the data received from the Internet resource; false to disable buffering. The default is true.
    /// </returns>
    [__DynamicallyInvokable]
    public virtual bool AllowReadStreamBuffering
    {
      [__DynamicallyInvokable] get
      {
        return false;
      }
      [__DynamicallyInvokable] set
      {
        if (value)
          throw new InvalidOperationException(SR.GetString("NotSupported"));
      }
    }

    private bool ExpectContinue
    {
      get
      {
        return (this._Booleans & HttpWebRequest.Booleans.ExpectContinue) > (HttpWebRequest.Booleans) 0;
      }
      set
      {
        if (value)
          this._Booleans = this._Booleans | HttpWebRequest.Booleans.ExpectContinue;
        else
          this._Booleans = this._Booleans & ~HttpWebRequest.Booleans.ExpectContinue;
      }
    }

    /// <summary>
    /// Gets a value that indicates whether a response has been received from an Internet resource.
    /// </summary>
    /// 
    /// <returns>
    /// true if a response has been received; otherwise, false.
    /// </returns>
    [__DynamicallyInvokable]
    public virtual bool HaveResponse
    {
      [__DynamicallyInvokable] get
      {
        if (this._ReadAResult != null)
          return this._ReadAResult.InternalPeekCompleted;
        return false;
      }
    }

    internal bool NtlmKeepAlive
    {
      get
      {
        return this.m_NtlmKeepAlive;
      }
      set
      {
        this.m_NtlmKeepAlive = value;
      }
    }

    internal bool NeedsToReadForResponse
    {
      get
      {
        return this.m_NeedsToReadForResponse;
      }
      set
      {
        this.m_NeedsToReadForResponse = value;
      }
    }

    internal bool BodyStarted
    {
      get
      {
        return this.m_BodyStarted;
      }
    }

    /// <summary>
    /// Gets or sets a value that indicates whether to make a persistent connection to the Internet resource.
    /// </summary>
    /// 
    /// <returns>
    /// true if the request to the Internet resource should contain a Connection HTTP header with the value Keep-alive; otherwise, false. The default is true.
    /// </returns>
    public bool KeepAlive
    {
      get
      {
        return this.m_KeepAlive;
      }
      set
      {
        this.m_KeepAlive = value;
      }
    }

    internal bool LockConnection
    {
      get
      {
        return this.m_LockConnection;
      }
      set
      {
        this.m_LockConnection = value;
      }
    }

    /// <summary>
    /// Gets or sets a value that indicates whether to pipeline the request to the Internet resource.
    /// </summary>
    /// 
    /// <returns>
    /// true if the request should be pipelined; otherwise, false. The default is true.
    /// </returns>
    public bool Pipelined
    {
      get
      {
        return this.m_Pipelined;
      }
      set
      {
        this.m_Pipelined = value;
      }
    }

    /// <summary>
    /// Gets or sets a value that indicates whether to send an Authorization header with the request.
    /// </summary>
    /// 
    /// <returns>
    /// true to send an  HTTP Authorization header with requests after authentication has taken place; otherwise, false. The default is false.
    /// </returns>
    public override bool PreAuthenticate
    {
      get
      {
        return this.m_PreAuthenticate;
      }
      set
      {
        this.m_PreAuthenticate = value;
      }
    }

    private bool ProxySet
    {
      get
      {
        return (this._Booleans & HttpWebRequest.Booleans.ProxySet) > (HttpWebRequest.Booleans) 0;
      }
      set
      {
        if (value)
          this._Booleans = this._Booleans | HttpWebRequest.Booleans.ProxySet;
        else
          this._Booleans = this._Booleans & ~HttpWebRequest.Booleans.ProxySet;
      }
    }

    private bool RequestSubmitted
    {
      get
      {
        return this.m_RequestSubmitted;
      }
    }

    internal bool Saw100Continue
    {
      get
      {
        return this.m_Saw100Continue;
      }
      set
      {
        this.m_Saw100Continue = value;
      }
    }

    /// <summary>
    /// Gets or sets a value that indicates whether to allow high-speed NTLM-authenticated connection sharing.
    /// </summary>
    /// 
    /// <returns>
    /// true to keep the authenticated connection open; otherwise, false.
    /// </returns>
    /// <PermissionSet><IPermission class="System.Net.WebPermission, System, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/></PermissionSet>
    public bool UnsafeAuthenticatedConnectionSharing
    {
      get
      {
        return (this._Booleans & HttpWebRequest.Booleans.UnsafeAuthenticatedConnectionSharing) > (HttpWebRequest.Booleans) 0;
      }
      set
      {
        ExceptionHelper.WebPermissionUnrestricted.Demand();
        if (value)
          this._Booleans = this._Booleans | HttpWebRequest.Booleans.UnsafeAuthenticatedConnectionSharing;
        else
          this._Booleans = this._Booleans & ~HttpWebRequest.Booleans.UnsafeAuthenticatedConnectionSharing;
      }
    }

    internal bool UnsafeOrProxyAuthenticatedConnectionSharing
    {
      get
      {
        if (!this.m_IsCurrentAuthenticationStateProxy)
          return this.UnsafeAuthenticatedConnectionSharing;
        return true;
      }
    }

    private bool IsVersionHttp10
    {
      get
      {
        return (this._Booleans & HttpWebRequest.Booleans.IsVersionHttp10) > (HttpWebRequest.Booleans) 0;
      }
      set
      {
        if (value)
          this._Booleans = this._Booleans | HttpWebRequest.Booleans.IsVersionHttp10;
        else
          this._Booleans = this._Booleans & ~HttpWebRequest.Booleans.IsVersionHttp10;
      }
    }

    /// <summary>
    /// Gets or sets a value that indicates whether to send data in segments to the Internet resource.
    /// </summary>
    /// 
    /// <returns>
    /// true to send data to the Internet resource in segments; otherwise, false. The default value is false.
    /// </returns>
    /// <exception cref="T:System.InvalidOperationException">The request has been started by calling the <see cref="M:System.Net.HttpWebRequest.GetRequestStream"/>, <see cref="M:System.Net.HttpWebRequest.BeginGetRequestStream(System.AsyncCallback,System.Object)"/>, <see cref="M:System.Net.HttpWebRequest.GetResponse"/>, or <see cref="M:System.Net.HttpWebRequest.BeginGetResponse(System.AsyncCallback,System.Object)"/> method. </exception>
    public bool SendChunked
    {
      get
      {
        return (this._Booleans & HttpWebRequest.Booleans.SendChunked) > (HttpWebRequest.Booleans) 0;
      }
      set
      {
        if (this.RequestSubmitted)
          throw new InvalidOperationException(SR.GetString("net_writestarted"));
        if (value)
          this._Booleans = this._Booleans | HttpWebRequest.Booleans.SendChunked;
        else
          this._Booleans = this._Booleans & ~HttpWebRequest.Booleans.SendChunked;
      }
    }

    /// <summary>
    /// Gets or sets the type of decompression that is used.
    /// </summary>
    /// 
    /// <returns>
    /// A T:System.Net.DecompressionMethods object that indicates the type of decompression that is used.
    /// </returns>
    /// <exception cref="T:System.InvalidOperationException">The object's current state does not allow this property to be set.</exception>
    public DecompressionMethods AutomaticDecompression
    {
      get
      {
        return this.m_AutomaticDecompression;
      }
      set
      {
        if (this.RequestSubmitted)
          throw new InvalidOperationException(SR.GetString("net_writestarted"));
        this.m_AutomaticDecompression = value;
      }
    }

    internal HttpWriteMode HttpWriteMode
    {
      get
      {
        return this._HttpWriteMode;
      }
      set
      {
        this._HttpWriteMode = value;
      }
    }

    /// <summary>
    /// Gets or sets the default cache policy for this request.
    /// </summary>
    /// 
    /// <returns>
    /// A <see cref="T:System.Net.Cache.HttpRequestCachePolicy"/> that specifies the cache policy in effect for this request when no other policy is applicable.
    /// </returns>
    /// <PermissionSet><IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Flags="ControlEvidence"/><IPermission class="System.Net.WebPermission, System, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/></PermissionSet>
    public new static RequestCachePolicy DefaultCachePolicy
    {
      get
      {
        return RequestCacheManager.GetBinding(Uri.UriSchemeHttp).Policy ?? WebRequest.DefaultCachePolicy;
      }
      set
      {
        ExceptionHelper.WebPermissionUnrestricted.Demand();
        RequestCacheBinding binding = RequestCacheManager.GetBinding(Uri.UriSchemeHttp);
        RequestCacheManager.SetBinding(Uri.UriSchemeHttp, new RequestCacheBinding(binding.Cache, binding.Validator, value));
      }
    }

    /// <summary>
    /// Gets or sets the default for the <see cref="P:System.Net.HttpWebRequest.MaximumResponseHeadersLength"/> property.
    /// </summary>
    /// 
    /// <returns>
    /// The length, in kilobytes (1024 bytes), of the default maximum for response headers received. The default configuration file sets this value to 64 kilobytes.
    /// </returns>
    /// <exception cref="T:System.ArgumentOutOfRangeException">The value is not equal to -1 and is less than zero. </exception><PermissionSet><IPermission class="System.Net.WebPermission, System, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/></PermissionSet>
    public static int DefaultMaximumResponseHeadersLength
    {
      get
      {
        return SettingsSectionInternal.Section.MaximumResponseHeadersLength;
      }
      set
      {
        ExceptionHelper.WebPermissionUnrestricted.Demand();
        if (value < 0 && value != -1)
          throw new ArgumentOutOfRangeException("value", SR.GetString("net_toosmall"));
        SettingsSectionInternal.Section.MaximumResponseHeadersLength = value;
      }
    }

    /// <summary>
    /// Gets or sets the default maximum length of an HTTP error response.
    /// </summary>
    /// 
    /// <returns>
    /// The default maximum length of an HTTP error response.
    /// </returns>
    /// <exception cref="T:System.ArgumentOutOfRangeException">The value is less than 0 and is not equal to -1. </exception>
    public static int DefaultMaximumErrorResponseLength
    {
      get
      {
        return SettingsSectionInternal.Section.MaximumErrorResponseLength;
      }
      set
      {
        ExceptionHelper.WebPermissionUnrestricted.Demand();
        if (value < 0 && value != -1)
          throw new ArgumentOutOfRangeException("value", SR.GetString("net_toosmall"));
        SettingsSectionInternal.Section.MaximumErrorResponseLength = value;
      }
    }

    /// <summary>
    /// Gets or sets the maximum allowed length of the response headers.
    /// </summary>
    /// 
    /// <returns>
    /// The length, in kilobytes (1024 bytes), of the response headers.
    /// </returns>
    /// <exception cref="T:System.InvalidOperationException">The property is set after the request has already been submitted. </exception><exception cref="T:System.ArgumentOutOfRangeException">The value is less than 0 and is not equal to -1. </exception>
    public int MaximumResponseHeadersLength
    {
      get
      {
        return this._MaximumResponseHeadersLength;
      }
      set
      {
        if (this.RequestSubmitted)
          throw new InvalidOperationException(SR.GetString("net_reqsubmitted"));
        if (value < 0 && value != -1)
          throw new ArgumentOutOfRangeException("value", SR.GetString("net_toosmall"));
        this._MaximumResponseHeadersLength = value;
      }
    }

    internal HttpAbortDelegate AbortDelegate
    {
      set
      {
        this._AbortDelegate = value;
      }
    }

    internal LazyAsyncResult ConnectionAsyncResult
    {
      get
      {
        return this._ConnectionAResult;
      }
    }

    internal LazyAsyncResult ConnectionReaderAsyncResult
    {
      get
      {
        return this._ConnectionReaderAResult;
      }
    }

    internal bool UserRetrievedWriteStream
    {
      get
      {
        if (this._WriteAResult != null)
          return this._WriteAResult.InternalPeekCompleted;
        return false;
      }
    }

    private bool IsOutstandingGetRequestStream
    {
      get
      {
        if (this._WriteAResult != null)
          return !this._WriteAResult.InternalPeekCompleted;
        return false;
      }
    }

    internal bool Async
    {
      get
      {
        return (uint) this._RequestIsAsync > 0U;
      }
      set
      {
        if (this._RequestIsAsync != TriState.Unspecified)
          return;
        this._RequestIsAsync = value ? TriState.True : TriState.False;
      }
    }

    internal UnlockConnectionDelegate UnlockConnectionDelegate
    {
      get
      {
        return this._UnlockDelegate;
      }
      set
      {
        this._UnlockDelegate = value;
      }
    }

    private bool UsesProxy
    {
      get
      {
        return this.ServicePoint.InternalProxyServicePoint;
      }
    }

    internal HttpStatusCode ResponseStatusCode
    {
      get
      {
        return this._HttpResponse.StatusCode;
      }
    }

    internal bool UsesProxySemantics
    {
      get
      {
        if (!this.ServicePoint.InternalProxyServicePoint)
          return false;
        if (this._Uri.Scheme == Uri.UriSchemeHttps || this.IsWebSocketRequest)
          return this.IsTunnelRequest;
        return true;
      }
    }

    internal Uri ChallengedUri
    {
      get
      {
        return this.CurrentAuthenticationState.ChallengedUri;
      }
    }

    internal AuthenticationState ProxyAuthenticationState
    {
      get
      {
        if (this._ProxyAuthenticationState == null)
          this._ProxyAuthenticationState = new AuthenticationState(true);
        return this._ProxyAuthenticationState;
      }
    }

    internal AuthenticationState ServerAuthenticationState
    {
      get
      {
        if (this._ServerAuthenticationState == null)
          this._ServerAuthenticationState = new AuthenticationState(false);
        return this._ServerAuthenticationState;
      }
      set
      {
        this._ServerAuthenticationState = value;
      }
    }

    internal AuthenticationState CurrentAuthenticationState
    {
      get
      {
        if (!this.m_IsCurrentAuthenticationStateProxy)
          return this._ServerAuthenticationState;
        return this._ProxyAuthenticationState;
      }
      set
      {
        this.m_IsCurrentAuthenticationStateProxy = this._ProxyAuthenticationState == value;
      }
    }

    /// <summary>
    /// Gets or sets the collection of security certificates that are associated with this request.
    /// </summary>
    /// 
    /// <returns>
    /// The <see cref="T:System.Security.Cryptography.X509Certificates.X509CertificateCollection"/> that contains the security certificates associated with this request.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">The value specified for a set operation is null. </exception>
    public X509CertificateCollection ClientCertificates
    {
      get
      {
        if (this._ClientCertificates == null)
          this._ClientCertificates = new X509CertificateCollection();
        return this._ClientCertificates;
      }
      set
      {
        if (value == null)
          throw new ArgumentNullException("value");
        this._ClientCertificates = value;
      }
    }

    /// <summary>
    /// Gets or sets the cookies associated with the request.
    /// </summary>
    /// 
    /// <returns>
    /// A <see cref="T:System.Net.CookieContainer"/> that contains the cookies associated with this request.
    /// </returns>
    [__DynamicallyInvokable]
    public virtual CookieContainer CookieContainer
    {
      [__DynamicallyInvokable] get
      {
        return this._CookieContainer;
      }
      [__DynamicallyInvokable] set
      {
        this._CookieContainer = value;
      }
    }

    /// <summary>
    /// Gets a value that indicates whether the request provides support for a <see cref="T:System.Net.CookieContainer"/>.
    /// </summary>
    /// 
    /// <returns>
    /// true if the request provides support for a <see cref="T:System.Net.CookieContainer"/>; otherwise, false.true if a <see cref="T:System.Net.CookieContainer"/> is supported; otherwise, false.
    /// </returns>
    [__DynamicallyInvokable]
    public virtual bool SupportsCookieContainer
    {
      [__DynamicallyInvokable] get
      {
        return true;
      }
    }

    /// <summary>
    /// Gets the original Uniform Resource Identifier (URI) of the request.
    /// </summary>
    /// 
    /// <returns>
    /// A <see cref="T:System.Uri"/> that contains the URI of the Internet resource passed to the <see cref="M:System.Net.WebRequest.Create(System.String)"/> method.
    /// </returns>
    [__DynamicallyInvokable]
    public override Uri RequestUri
    {
      [__DynamicallyInvokable] get
      {
        return this._OriginUri;
      }
    }

    /// <summary>
    /// Gets or sets the Content-length HTTP header.
    /// </summary>
    /// 
    /// <returns>
    /// The number of bytes of data to send to the Internet resource. The default is -1, which indicates the property has not been set and that there is no request data to send.
    /// </returns>
    /// <exception cref="T:System.InvalidOperationException">The request has been started by calling the <see cref="M:System.Net.HttpWebRequest.GetRequestStream"/>, <see cref="M:System.Net.HttpWebRequest.BeginGetRequestStream(System.AsyncCallback,System.Object)"/>, <see cref="M:System.Net.HttpWebRequest.GetResponse"/>, or <see cref="M:System.Net.HttpWebRequest.BeginGetResponse(System.AsyncCallback,System.Object)"/> method. </exception><exception cref="T:System.ArgumentOutOfRangeException">The new <see cref="P:System.Net.HttpWebRequest.ContentLength"/> value is less than 0. </exception>
    public override long ContentLength
    {
      get
      {
        return this._ContentLength;
      }
      set
      {
        if (this.RequestSubmitted)
          throw new InvalidOperationException(SR.GetString("net_writestarted"));
        if (value < 0L)
          throw new ArgumentOutOfRangeException("value", SR.GetString("net_clsmall"));
        this._ContentLength = value;
        this._originalContentLength = value;
      }
    }

    /// <summary>
    /// Gets or sets the time-out value in milliseconds for the <see cref="M:System.Net.HttpWebRequest.GetResponse"/> and <see cref="M:System.Net.HttpWebRequest.GetRequestStream"/> methods.
    /// </summary>
    /// 
    /// <returns>
    /// The number of milliseconds to wait before the request times out. The default value is 100,000 milliseconds (100 seconds).
    /// </returns>
    /// <exception cref="T:System.ArgumentOutOfRangeException">The value specified is less than zero and is not <see cref="F:System.Threading.Timeout.Infinite"/>.</exception>
    public override int Timeout
    {
      get
      {
        return this._Timeout;
      }
      set
      {
        if (value < 0 && value != -1)
          throw new ArgumentOutOfRangeException("value", SR.GetString("net_io_timeout_use_ge_zero"));
        if (this._Timeout == value)
          return;
        this._Timeout = value;
        this._TimerQueue = (TimerThread.Queue) null;
      }
    }

    private TimerThread.Queue TimerQueue
    {
      get
      {
        TimerThread.Queue queue = this._TimerQueue;
        if (queue == null)
        {
          queue = TimerThread.GetOrCreateQueue(this._Timeout == 0 ? 1 : this._Timeout);
          this._TimerQueue = queue;
        }
        return queue;
      }
    }

    /// <summary>
    /// Gets or sets a time-out in milliseconds when writing to or reading from a stream.
    /// </summary>
    /// 
    /// <returns>
    /// The number of milliseconds before the writing or reading times out. The default value is 300,000 milliseconds (5 minutes).
    /// </returns>
    /// <exception cref="T:System.InvalidOperationException">The request has already been sent. </exception><exception cref="T:System.ArgumentOutOfRangeException">The value specified for a set operation is less than or equal to zero and is not equal to <see cref="F:System.Threading.Timeout.Infinite"/></exception>
    public int ReadWriteTimeout
    {
      get
      {
        return this._ReadWriteTimeout;
      }
      set
      {
        if (this.RequestSubmitted)
          throw new InvalidOperationException(SR.GetString("net_reqsubmitted"));
        if (value <= 0 && value != -1)
          throw new ArgumentOutOfRangeException("value", SR.GetString("net_io_timeout_use_gt_zero"));
        this._ReadWriteTimeout = value;
      }
    }

    /// <summary>
    /// Gets or sets a timeout, in milliseconds, to wait until the 100-Continue is received from the server.
    /// </summary>
    /// 
    /// <returns>
    /// The timeout, in milliseconds, to wait until the 100-Continue is received.
    /// </returns>
    [__DynamicallyInvokable]
    public int ContinueTimeout
    {
      [__DynamicallyInvokable] get
      {
        return this.m_ContinueTimeout;
      }
      [__DynamicallyInvokable] set
      {
        if (this.RequestSubmitted)
          throw new InvalidOperationException(SR.GetString("net_reqsubmitted"));
        if (value < 0 && value != -1)
          throw new ArgumentOutOfRangeException("value", SR.GetString("net_io_timeout_use_ge_zero"));
        if (this.m_ContinueTimeout == value)
          return;
        this.m_ContinueTimeout = value;
        if (value == 350)
          this.m_ContinueTimerQueue = HttpWebRequest.s_ContinueTimerQueue;
        else
          this.m_ContinueTimerQueue = (TimerThread.Queue) null;
      }
    }

    private TimerThread.Queue ContinueTimerQueue
    {
      get
      {
        if (this.m_ContinueTimerQueue == null)
          this.m_ContinueTimerQueue = TimerThread.GetOrCreateQueue(this.m_ContinueTimeout == 0 ? 1 : this.m_ContinueTimeout);
        return this.m_ContinueTimerQueue;
      }
    }

    internal bool HeadersCompleted
    {
      get
      {
        return this.m_HeadersCompleted;
      }
      set
      {
        this.m_HeadersCompleted = value;
      }
    }

    private bool CanGetRequestStream
    {
      get
      {
        return !this.CurrentMethod.ContentBodyNotAllowed;
      }
    }

    internal bool CanGetResponseStream
    {
      get
      {
        return !this.CurrentMethod.ExpectNoContentResponse;
      }
    }

    internal bool RequireBody
    {
      get
      {
        return this.CurrentMethod.RequireContentBody;
      }
    }

    internal bool HasEntityBody
    {
      get
      {
        if (this.HttpWriteMode == HttpWriteMode.Chunked || this.HttpWriteMode == HttpWriteMode.Buffer)
          return true;
        if (this.HttpWriteMode == HttpWriteMode.ContentLength)
          return this.ContentLength > 0L;
        return false;
      }
    }

    /// <summary>
    /// Gets the Uniform Resource Identifier (URI) of the Internet resource that actually responds to the request.
    /// </summary>
    /// 
    /// <returns>
    /// A <see cref="T:System.Uri"/> that identifies the Internet resource that actually responds to the request. The default is the URI used by the <see cref="M:System.Net.WebRequest.Create(System.String)"/> method to initialize the request.
    /// </returns>
    public Uri Address
    {
      get
      {
        return this._Uri;
      }
    }

    /// <summary>
    /// Gets or sets the delegate method called when an HTTP 100-continue response is received from the Internet resource.
    /// </summary>
    /// 
    /// <returns>
    /// A delegate that implements the callback method that executes when an HTTP Continue response is returned from the Internet resource. The default value is null.
    /// </returns>
    public HttpContinueDelegate ContinueDelegate
    {
      get
      {
        return this._ContinueDelegate;
      }
      set
      {
        this._ContinueDelegate = value;
      }
    }

    /// <summary>
    /// Gets the service point to use for the request.
    /// </summary>
    /// 
    /// <returns>
    /// A <see cref="T:System.Net.ServicePoint"/> that represents the network connection to the Internet resource.
    /// </returns>
    /// <PermissionSet><IPermission class="System.Security.Permissions.EnvironmentPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Flags="UnmanagedCode, ControlEvidence"/></PermissionSet>
    public ServicePoint ServicePoint
    {
      get
      {
        return this.FindServicePoint(false);
      }
    }

    /// <summary>
    /// Get or set the Host header value to use in an HTTP request independent from the request URI.
    /// </summary>
    /// 
    /// <returns>
    /// The Host header value in the HTTP request.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">The Host header cannot be set to null. </exception><exception cref="T:System.ArgumentException">The Host header cannot be set to an invalid value. </exception><exception cref="T:System.InvalidOperationException">The Host header cannot be set after the <see cref="T:System.Net.HttpWebRequest"/> has already started to be sent. </exception>
    public string Host
    {
      get
      {
        if (this.UseCustomHost)
          return HttpWebRequest.GetHostAndPortString(this._HostUri.Host, this._HostUri.Port, this._HostHasPort);
        return HttpWebRequest.GetHostAndPortString(this._Uri.Host, this._Uri.Port, !this._Uri.IsDefaultPort);
      }
      set
      {
        if (this.RequestSubmitted)
          throw new InvalidOperationException(SR.GetString("net_writestarted"));
        if (value == null)
          throw new ArgumentNullException();
        Uri hostUri;
        if (value.IndexOf('/') != -1 || !this.TryGetHostUri(value, out hostUri))
          throw new ArgumentException(SR.GetString("net_invalid_host"));
        this.CheckConnectPermission(hostUri, false);
        this._HostUri = hostUri;
        if (!this._HostUri.IsDefaultPort)
          this._HostHasPort = true;
        else if (value.IndexOf(':') == -1)
        {
          this._HostHasPort = false;
        }
        else
        {
          int num = value.IndexOf(']');
          if (num == -1)
            this._HostHasPort = true;
          else
            this._HostHasPort = value.LastIndexOf(':') > num;
        }
      }
    }

    internal bool UseCustomHost
    {
      get
      {
        if (this._HostUri != (Uri) null)
          return !this._RedirectedToDifferentHost;
        return false;
      }
    }

    /// <summary>
    /// Gets or sets the maximum number of redirects that the request follows.
    /// </summary>
    /// 
    /// <returns>
    /// The maximum number of redirection responses that the request follows. The default value is 50.
    /// </returns>
    /// <exception cref="T:System.ArgumentException">The value is set to 0 or less. </exception>
    public int MaximumAutomaticRedirections
    {
      get
      {
        return this._MaximumAllowedRedirections;
      }
      set
      {
        if (value <= 0)
          throw new ArgumentException(SR.GetString("net_toosmall"), "value");
        this._MaximumAllowedRedirections = value;
      }
    }

    /// <summary>
    /// Gets or sets the method for the request.
    /// </summary>
    /// 
    /// <returns>
    /// The request method to use to contact the Internet resource. The default value is GET.
    /// </returns>
    /// <exception cref="T:System.ArgumentException">No method is supplied.-or- The method string contains invalid characters. </exception>
    [__DynamicallyInvokable]
    public override string Method
    {
      [__DynamicallyInvokable] get
      {
        return this._OriginVerb.Name;
      }
      [__DynamicallyInvokable] set
      {
        if (ValidationHelper.IsBlankString(value))
          throw new ArgumentException(SR.GetString("net_badmethod"), "value");
        if (ValidationHelper.IsInvalidHttpString(value))
          throw new ArgumentException(SR.GetString("net_badmethod"), "value");
        this._OriginVerb = KnownHttpVerb.Parse(value);
      }
    }

    internal KnownHttpVerb CurrentMethod
    {
      get
      {
        if (this._Verb == null)
          return this._OriginVerb;
        return this._Verb;
      }
      set
      {
        this._Verb = value;
      }
    }

    /// <summary>
    /// Gets or sets authentication information for the request.
    /// </summary>
    /// 
    /// <returns>
    /// An <see cref="T:System.Net.ICredentials"/> that contains the authentication credentials associated with the request. The default is null.
    /// </returns>
    /// <PermissionSet><IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Flags="UnmanagedCode, ControlEvidence"/></PermissionSet>
    [__DynamicallyInvokable]
    public override ICredentials Credentials
    {
      [__DynamicallyInvokable] get
      {
        return this._AuthInfo;
      }
      [__DynamicallyInvokable] set
      {
        this._AuthInfo = value;
      }
    }

    /// <summary>
    /// Gets or sets a <see cref="T:System.Boolean"/> value that controls whether default credentials are sent with requests.
    /// </summary>
    /// 
    /// <returns>
    /// true if the default credentials are used; otherwise false. The default value is false.
    /// </returns>
    /// <exception cref="T:System.InvalidOperationException">You attempted to set this property after the request was sent.</exception><PermissionSet><IPermission class="System.Security.Permissions.EnvironmentPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Read="USERNAME"/></PermissionSet>
    [__DynamicallyInvokable]
    public override bool UseDefaultCredentials
    {
      [__DynamicallyInvokable] get
      {
        return this.Credentials is SystemNetworkCredential;
      }
      [__DynamicallyInvokable] set
      {
        if (this.RequestSubmitted)
          throw new InvalidOperationException(SR.GetString("net_writestarted"));
        this._AuthInfo = value ? CredentialCache.DefaultCredentials : (ICredentials) null;
      }
    }

    internal bool IsTunnelRequest
    {
      get
      {
        return (this._Booleans & HttpWebRequest.Booleans.IsTunnelRequest) > (HttpWebRequest.Booleans) 0;
      }
      set
      {
        if (value)
          this._Booleans = this._Booleans | HttpWebRequest.Booleans.IsTunnelRequest;
        else
          this._Booleans = this._Booleans & ~HttpWebRequest.Booleans.IsTunnelRequest;
      }
    }

    internal bool IsWebSocketRequest
    {
      get
      {
        return (this._Booleans & HttpWebRequest.Booleans.IsWebSocketRequest) > (HttpWebRequest.Booleans) 0;
      }
      private set
      {
        if (value)
          this._Booleans = this._Booleans | HttpWebRequest.Booleans.IsWebSocketRequest;
        else
          this._Booleans = this._Booleans & ~HttpWebRequest.Booleans.IsWebSocketRequest;
      }
    }

    /// <summary>
    /// Gets or sets the name of the connection group for the request.
    /// </summary>
    /// 
    /// <returns>
    /// The name of the connection group for this request. The default value is null.
    /// </returns>
    public override string ConnectionGroupName
    {
      get
      {
        return this._ConnectionGroupName;
      }
      set
      {
        if (this.IsWebSocketRequest)
          return;
        this._ConnectionGroupName = value;
      }
    }

    internal bool InternalConnectionGroup
    {
      set
      {
        this.m_InternalConnectionGroup = value;
      }
    }

    /// <summary>
    /// Specifies a collection of the name/value pairs that make up the HTTP headers.
    /// </summary>
    /// 
    /// <returns>
    /// A <see cref="T:System.Net.WebHeaderCollection"/> that contains the name/value pairs that make up the headers for the HTTP request.
    /// </returns>
    /// <exception cref="T:System.InvalidOperationException">The request has been started by calling the <see cref="M:System.Net.HttpWebRequest.GetRequestStream"/>, <see cref="M:System.Net.HttpWebRequest.BeginGetRequestStream(System.AsyncCallback,System.Object)"/>, <see cref="M:System.Net.HttpWebRequest.GetResponse"/>, or <see cref="M:System.Net.HttpWebRequest.BeginGetResponse(System.AsyncCallback,System.Object)"/> method. </exception><PermissionSet><IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Flags="UnmanagedCode, ControlEvidence"/></PermissionSet>
    [__DynamicallyInvokable]
    public override WebHeaderCollection Headers
    {
      [__DynamicallyInvokable] get
      {
        return this._HttpRequestHeaders;
      }
      [__DynamicallyInvokable] set
      {
        if (this.RequestSubmitted)
          throw new InvalidOperationException(SR.GetString("net_reqsubmitted"));
        WebHeaderCollection headerCollection1 = value;
        WebHeaderCollection headerCollection2 = new WebHeaderCollection(WebHeaderCollectionType.HttpWebRequest);
        foreach (string name in headerCollection1.AllKeys)
          headerCollection2.Add(name, headerCollection1[name]);
        this._HttpRequestHeaders = headerCollection2;
      }
    }

    /// <summary>
    /// Gets or sets proxy information for the request.
    /// </summary>
    /// 
    /// <returns>
    /// The <see cref="T:System.Net.IWebProxy"/> object to use to proxy the request. The default value is set by calling the <see cref="P:System.Net.GlobalProxySelection.Select"/> property.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException"><see cref="P:System.Net.HttpWebRequest.Proxy"/> is set to null. </exception><exception cref="T:System.InvalidOperationException">The request has been started by calling <see cref="M:System.Net.HttpWebRequest.GetRequestStream"/>, <see cref="M:System.Net.HttpWebRequest.BeginGetRequestStream(System.AsyncCallback,System.Object)"/>, <see cref="M:System.Net.HttpWebRequest.GetResponse"/>, or <see cref="M:System.Net.HttpWebRequest.BeginGetResponse(System.AsyncCallback,System.Object)"/>. </exception><exception cref="T:System.Security.SecurityException">The caller does not have permission for the requested operation. </exception><PermissionSet><IPermission class="System.Security.Permissions.EnvironmentPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Flags="UnmanagedCode, ControlEvidence"/><IPermission class="System.Net.WebPermission, System, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/></PermissionSet>
    public override IWebProxy Proxy
    {
      get
      {
        ExceptionHelper.WebPermissionUnrestricted.Demand();
        return this._Proxy;
      }
      set
      {
        ExceptionHelper.WebPermissionUnrestricted.Demand();
        if (this.RequestSubmitted)
          throw new InvalidOperationException(SR.GetString("net_reqsubmitted"));
        this.InternalProxy = value;
      }
    }

    internal IWebProxy InternalProxy
    {
      get
      {
        return this._Proxy;
      }
      set
      {
        this.ProxySet = true;
        this._Proxy = value;
        if (this._ProxyChain != null)
          this._ProxyChain.Dispose();
        this._ProxyChain = (ProxyChain) null;
        this.FindServicePoint(true);
      }
    }

    /// <summary>
    /// Gets or sets the version of HTTP to use for the request.
    /// </summary>
    /// 
    /// <returns>
    /// The HTTP version to use for the request. The default is <see cref="F:System.Net.HttpVersion.Version11"/>.
    /// </returns>
    /// <exception cref="T:System.ArgumentException">The HTTP version is set to a value other than 1.0 or 1.1. </exception>
    public Version ProtocolVersion
    {
      get
      {
        if (!this.IsVersionHttp10)
          return HttpVersion.Version11;
        return HttpVersion.Version10;
      }
      set
      {
        if (value.Equals(HttpVersion.Version11))
        {
          this.IsVersionHttp10 = false;
        }
        else
        {
          if (!value.Equals(HttpVersion.Version10))
            throw new ArgumentException(SR.GetString("net_wrongversion"), "value");
          this.IsVersionHttp10 = true;
        }
      }
    }

    /// <summary>
    /// Gets or sets the value of the Content-type HTTP header.
    /// </summary>
    /// 
    /// <returns>
    /// The value of the Content-type HTTP header. The default value is null.
    /// </returns>
    [__DynamicallyInvokable]
    public override string ContentType
    {
      [__DynamicallyInvokable] get
      {
        return this._HttpRequestHeaders["Content-Type"];
      }
      [__DynamicallyInvokable] set
      {
        this.SetSpecialHeaders("Content-Type", value);
      }
    }

    /// <summary>
    /// Gets or sets the media type of the request.
    /// </summary>
    /// 
    /// <returns>
    /// The media type of the request. The default value is null.
    /// </returns>
    public string MediaType
    {
      get
      {
        return this._MediaType;
      }
      set
      {
        this._MediaType = value;
      }
    }

    /// <summary>
    /// Gets or sets the value of the Transfer-encoding HTTP header.
    /// </summary>
    /// 
    /// <returns>
    /// The value of the Transfer-encoding HTTP header. The default value is null.
    /// </returns>
    /// <exception cref="T:System.InvalidOperationException"><see cref="P:System.Net.HttpWebRequest.TransferEncoding"/> is set when <see cref="P:System.Net.HttpWebRequest.SendChunked"/> is false. </exception><exception cref="T:System.ArgumentException"><see cref="P:System.Net.HttpWebRequest.TransferEncoding"/> is set to the value "Chunked". </exception>
    public string TransferEncoding
    {
      get
      {
        return this._HttpRequestHeaders["Transfer-Encoding"];
      }
      set
      {
        if (ValidationHelper.IsBlankString(value))
        {
          this._HttpRequestHeaders.RemoveInternal("Transfer-Encoding");
        }
        else
        {
          if (value.ToLower(CultureInfo.InvariantCulture).IndexOf("chunked") != -1)
            throw new ArgumentException(SR.GetString("net_nochunked"), "value");
          if (!this.SendChunked)
            throw new InvalidOperationException(SR.GetString("net_needchunked"));
          this._HttpRequestHeaders.CheckUpdate("Transfer-Encoding", value);
        }
      }
    }

    /// <summary>
    /// Gets or sets the value of the Connection HTTP header.
    /// </summary>
    /// 
    /// <returns>
    /// The value of the Connection HTTP header. The default value is null.
    /// </returns>
    /// <exception cref="T:System.ArgumentException">The value of <see cref="P:System.Net.HttpWebRequest.Connection"/> is set to Keep-alive or Close. </exception>
    public string Connection
    {
      get
      {
        return this._HttpRequestHeaders["Connection"];
      }
      set
      {
        if (ValidationHelper.IsBlankString(value))
        {
          this._HttpRequestHeaders.RemoveInternal("Connection");
        }
        else
        {
          string str1 = value.ToLower(CultureInfo.InvariantCulture);
          string str2 = "keep-alive";
          bool flag1 = str1.IndexOf(str2) != -1;
          string str3 = "close";
          bool flag2 = str1.IndexOf(str3) != -1;
          if (flag1 | flag2)
            throw new ArgumentException(SR.GetString("net_connarg"), "value");
          this._HttpRequestHeaders.CheckUpdate("Connection", value);
        }
      }
    }

    /// <summary>
    /// Gets or sets the value of the Accept HTTP header.
    /// </summary>
    /// 
    /// <returns>
    /// The value of the Accept HTTP header. The default value is null.
    /// </returns>
    [__DynamicallyInvokable]
    public string Accept
    {
      [__DynamicallyInvokable] get
      {
        return this._HttpRequestHeaders["Accept"];
      }
      [__DynamicallyInvokable] set
      {
        this.SetSpecialHeaders("Accept", value);
      }
    }

    /// <summary>
    /// Gets or sets the value of the Referer HTTP header.
    /// </summary>
    /// 
    /// <returns>
    /// The value of the Referer HTTP header. The default value is null.
    /// </returns>
    public string Referer
    {
      get
      {
        return this._HttpRequestHeaders["Referer"];
      }
      set
      {
        this.SetSpecialHeaders("Referer", value);
      }
    }

    /// <summary>
    /// Gets or sets the value of the User-agent HTTP header.
    /// </summary>
    /// 
    /// <returns>
    /// The value of the User-agent HTTP header. The default value is null.NoteThe value for this property is stored in <see cref="T:System.Net.WebHeaderCollection"/>. If WebHeaderCollection is set, the property value is lost.
    /// </returns>
    public string UserAgent
    {
      get
      {
        return this._HttpRequestHeaders["User-Agent"];
      }
      set
      {
        this.SetSpecialHeaders("User-Agent", value);
      }
    }

    /// <summary>
    /// Gets or sets the value of the Expect HTTP header.
    /// </summary>
    /// 
    /// <returns>
    /// The contents of the Expect HTTP header. The default value is null.NoteThe value for this property is stored in <see cref="T:System.Net.WebHeaderCollection"/>. If WebHeaderCollection is set, the property value is lost.
    /// </returns>
    /// <exception cref="T:System.ArgumentException">Expect is set to a string that contains "100-continue" as a substring. </exception>
    public string Expect
    {
      get
      {
        return this._HttpRequestHeaders["Expect"];
      }
      set
      {
        if (ValidationHelper.IsBlankString(value))
        {
          this._HttpRequestHeaders.RemoveInternal("Expect");
        }
        else
        {
          if (value.ToLower(CultureInfo.InvariantCulture).IndexOf("100-continue") != -1)
            throw new ArgumentException(SR.GetString("net_no100"), "value");
          this._HttpRequestHeaders.CheckUpdate("Expect", value);
        }
      }
    }

    /// <summary>
    /// Gets or sets the value of the If-Modified-Since HTTP header.
    /// </summary>
    /// 
    /// <returns>
    /// A <see cref="T:System.DateTime"/> that contains the contents of the If-Modified-Since HTTP header. The default value is the current date and time.
    /// </returns>
    /// <PermissionSet><IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Flags="UnmanagedCode, ControlEvidence"/></PermissionSet>
    public DateTime IfModifiedSince
    {
      get
      {
        return this.GetDateHeaderHelper("If-Modified-Since");
      }
      set
      {
        this.SetDateHeaderHelper("If-Modified-Since", value);
      }
    }

    /// <summary>
    /// Get or set the Date HTTP header value to use in an HTTP request.
    /// </summary>
    /// 
    /// <returns>
    /// The Date header value in the HTTP request.
    /// </returns>
    public DateTime Date
    {
      get
      {
        return this.GetDateHeaderHelper("Date");
      }
      set
      {
        this.SetDateHeaderHelper("Date", value);
      }
    }

    internal byte[] WriteBuffer
    {
      get
      {
        return this._WriteBuffer;
      }
    }

    internal int WriteBufferLength
    {
      get
      {
        return this._WriteBufferLength;
      }
    }

    internal int RequestContinueCount
    {
      get
      {
        return this._RequestContinueCount;
      }
    }

    private bool IdentityRequired
    {
      get
      {
        if (this.Credentials == null)
          return false;
        if (this.Credentials is SystemNetworkCredential)
          return true;
        if (this.Credentials is NetworkCredential)
          return false;
        CredentialCache credentialCache;
        if ((credentialCache = this.Credentials as CredentialCache) != null)
          return credentialCache.IsDefaultInCache;
        return true;
      }
    }

    internal ServerCertValidationCallback ServerCertValidationCallback { get; private set; }

    /// <summary>
    /// Gets or sets a callback function to validate the server certificate.
    /// </summary>
    /// 
    /// <returns>
    /// A callback function to validate the server certificate.A callback function to validate the server certificate.
    /// </returns>
    public RemoteCertificateValidationCallback ServerCertificateValidationCallback
    {
      get
      {
        if (this.ServerCertValidationCallback == null)
          return (RemoteCertificateValidationCallback) null;
        return this.ServerCertValidationCallback.ValidationCallback;
      }
      set
      {
        ExceptionHelper.InfrastructurePermission.Demand();
        if (value == null)
          this.ServerCertValidationCallback = (ServerCertValidationCallback) null;
        else
          this.ServerCertValidationCallback = new ServerCertValidationCallback(value);
      }
    }

    private static string UniqueGroupId
    {
      get
      {
        return Interlocked.Increment(ref HttpWebRequest.s_UniqueGroupId).ToString((IFormatProvider) NumberFormatInfo.InvariantInfo);
      }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:System.Net.HttpWebRequest"/> class.
    /// </summary>
    [Obsolete("This API supports the .NET Framework infrastructure and is not intended to be used directly from your code.", true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public HttpWebRequest()
    {
    }

    internal HttpWebRequest(Uri uri, ServicePoint servicePoint)
    {
      if (Logging.On)
        Logging.Enter(Logging.Web, (object) this, "HttpWebRequest", (object) uri);
      this.CheckConnectPermission(uri, false);
      this.m_StartTimestamp = NetworkingPerfCounters.GetTimestamp();
      NetworkingPerfCounters.Instance.Increment(NetworkingPerfCounterName.HttpWebRequestCreated);
      this._HttpRequestHeaders = new WebHeaderCollection(WebHeaderCollectionType.HttpWebRequest);
      this._Proxy = WebRequest.InternalDefaultWebProxy;
      this._HttpWriteMode = HttpWriteMode.Unknown;
      this._MaximumAllowedRedirections = 50;
      this._Timeout = 100000;
      this._TimerQueue = WebRequest.DefaultTimerQueue;
      this._ReadWriteTimeout = 300000;
      this._MaximumResponseHeadersLength = HttpWebRequest.DefaultMaximumResponseHeadersLength;
      this._ContentLength = -1L;
      this._originalContentLength = -1L;
      this._OriginVerb = KnownHttpVerb.Get;
      this._OriginUri = uri;
      this._Uri = this._OriginUri;
      this._ServicePoint = servicePoint;
      this._RequestIsAsync = TriState.Unspecified;
      this.m_ContinueTimeout = 350;
      this.m_ContinueTimerQueue = HttpWebRequest.s_ContinueTimerQueue;
      this.SetupCacheProtocol(this._OriginUri);
      if (!Logging.On)
        return;
      Logging.Exit(Logging.Web, (object) this, "HttpWebRequest", (string) null);
    }

    internal HttpWebRequest(Uri proxyUri, Uri requestUri, HttpWebRequest orginalRequest)
      : this(proxyUri, (ServicePoint) null)
    {
      this._OriginVerb = KnownHttpVerb.Parse("CONNECT");
      this.Pipelined = false;
      this._OriginUri = requestUri;
      this.IsTunnelRequest = true;
      this._ConnectionGroupName = ServicePointManager.SpecialConnectGroupName + "(" + HttpWebRequest.UniqueGroupId + ")";
      this.m_InternalConnectionGroup = true;
      this.ServerAuthenticationState = new AuthenticationState(true);
      this.CacheProtocol = (RequestCacheProtocol) null;
      this.m_ContinueTimeout = 350;
      this.m_ContinueTimerQueue = HttpWebRequest.s_ContinueTimerQueue;
    }

    internal HttpWebRequest(Uri uri, bool returnResponseOnFailureStatusCode, string connectionGroupName, Action<Stream> resendRequestContent)
      : this(uri, (ServicePoint) null)
    {
      if (Logging.On)
        Logging.Enter(Logging.Web, (object) this, "HttpWebRequest", "uri: '" + (object) uri + "', connectionGroupName: '" + connectionGroupName + "'");
      this._returnResponseOnFailureStatusCode = returnResponseOnFailureStatusCode;
      this._resendRequestContent = resendRequestContent;
      this._Booleans = this._Booleans & ~HttpWebRequest.Booleans.AllowWriteStreamBuffering;
      this.m_InternalConnectionGroup = true;
      this._ConnectionGroupName = connectionGroupName;
      if (!Logging.On)
        return;
      Logging.Exit(Logging.Web, (object) this, "HttpWebRequest", (string) null);
    }

    internal HttpWebRequest(Uri uri, ServicePoint servicePoint, bool isWebSocketRequest, string connectionGroupName)
      : this(uri, servicePoint)
    {
      this.IsWebSocketRequest = isWebSocketRequest;
      this._ConnectionGroupName = connectionGroupName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:System.Net.HttpWebRequest"/> class from the specified instances of the <see cref="T:System.Runtime.Serialization.SerializationInfo"/> and <see cref="T:System.Runtime.Serialization.StreamingContext"/> classes.
    /// </summary>
    /// <param name="serializationInfo">A <see cref="T:System.Runtime.Serialization.SerializationInfo"/> object that contains the information required to serialize the new <see cref="T:System.Net.HttpWebRequest"/> object. </param><param name="streamingContext">A <see cref="T:System.Runtime.Serialization.StreamingContext"/> object that contains the source and destination of the serialized stream associated with the new <see cref="T:System.Net.HttpWebRequest"/> object. </param>
    [Obsolete("Serialization is obsoleted for this type.  http://go.microsoft.com/fwlink/?linkid=14202")]
    [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
    protected HttpWebRequest(SerializationInfo serializationInfo, StreamingContext streamingContext)
      : base(serializationInfo, streamingContext)
    {
      ExceptionHelper.WebPermissionUnrestricted.Demand();
      if (Logging.On)
        Logging.Enter(Logging.Web, (object) this, "HttpWebRequest", (object) serializationInfo);
      this._HttpRequestHeaders = (WebHeaderCollection) serializationInfo.GetValue("_HttpRequestHeaders", typeof (WebHeaderCollection));
      this._Proxy = (IWebProxy) serializationInfo.GetValue("_Proxy", typeof (IWebProxy));
      this.KeepAlive = serializationInfo.GetBoolean("_KeepAlive");
      this.Pipelined = serializationInfo.GetBoolean("_Pipelined");
      this.AllowAutoRedirect = serializationInfo.GetBoolean("_AllowAutoRedirect");
      if (!serializationInfo.GetBoolean("_AllowWriteStreamBuffering"))
        this._Booleans = this._Booleans & ~HttpWebRequest.Booleans.AllowWriteStreamBuffering;
      this.HttpWriteMode = (HttpWriteMode) serializationInfo.GetInt32("_HttpWriteMode");
      this._MaximumAllowedRedirections = serializationInfo.GetInt32("_MaximumAllowedRedirections");
      this._AutoRedirects = serializationInfo.GetInt32("_AutoRedirects");
      this._Timeout = serializationInfo.GetInt32("_Timeout");
      this.m_ContinueTimeout = 350;
      this.m_ContinueTimerQueue = HttpWebRequest.s_ContinueTimerQueue;
      try
      {
        this._ReadWriteTimeout = serializationInfo.GetInt32("_ReadWriteTimeout");
      }
      catch
      {
        this._ReadWriteTimeout = 300000;
      }
      try
      {
        this._MaximumResponseHeadersLength = serializationInfo.GetInt32("_MaximumResponseHeadersLength");
      }
      catch
      {
        this._MaximumResponseHeadersLength = HttpWebRequest.DefaultMaximumResponseHeadersLength;
      }
      this._ContentLength = serializationInfo.GetInt64("_ContentLength");
      this._MediaType = serializationInfo.GetString("_MediaType");
      this._OriginVerb = KnownHttpVerb.Parse(serializationInfo.GetString("_OriginVerb"));
      this._ConnectionGroupName = serializationInfo.GetString("_ConnectionGroupName");
      this.ProtocolVersion = (Version) serializationInfo.GetValue("_Version", typeof (Version));
      this._OriginUri = (Uri) serializationInfo.GetValue("_OriginUri", typeof (Uri));
      this.SetupCacheProtocol(this._OriginUri);
      if (!Logging.On)
        return;
      Logging.Exit(Logging.Web, (object) this, "HttpWebRequest", (string) null);
    }

    private bool SetRequestSubmitted()
    {
      int num = this.RequestSubmitted ? 1 : 0;
      this.m_RequestSubmitted = true;
      return num != 0;
    }

    internal string AuthHeader(HttpResponseHeader header)
    {
      if (this._HttpResponse == null)
        return (string) null;
      return this._HttpResponse.Headers[header];
    }

    internal long SwitchToContentLength()
    {
      if (this.HaveResponse)
        return -1;
      if (this.HttpWriteMode == HttpWriteMode.Chunked)
      {
        ConnectStream connectStream = this._OldSubmitWriteStream ?? this._SubmitWriteStream;
        if (connectStream.Connection != null && connectStream.Connection.IISVersion >= 6)
          return -1;
      }
      long num1 = -1;
      long num2 = this._ContentLength;
      if (this.HttpWriteMode != HttpWriteMode.None)
      {
        if (this.HttpWriteMode == HttpWriteMode.Buffer)
        {
          this._ContentLength = (long) this._SubmitWriteStream.BufferedData.Length;
          this.m_OriginallyBuffered = true;
          this.HttpWriteMode = HttpWriteMode.ContentLength;
          return -1;
        }
        if (this.NtlmKeepAlive && this._OldSubmitWriteStream == null)
        {
          this._ContentLength = 0L;
          this._SubmitWriteStream.SuppressWrite = true;
          if (!this._SubmitWriteStream.BufferOnly)
            num1 = num2;
          if (this.HttpWriteMode == HttpWriteMode.Chunked)
          {
            this.HttpWriteMode = HttpWriteMode.ContentLength;
            this._SubmitWriteStream.SwitchToContentLength();
            num1 = -2L;
            this._HttpRequestHeaders.RemoveInternal("Transfer-Encoding");
          }
        }
        if (this._OldSubmitWriteStream != null)
        {
          if (this.NtlmKeepAlive)
            this._ContentLength = 0L;
          else if (this._ContentLength == 0L || this.HttpWriteMode == HttpWriteMode.Chunked)
          {
            if (this._resendRequestContent == null)
            {
              if (this._OldSubmitWriteStream.BufferedData != null)
                this._ContentLength = (long) this._OldSubmitWriteStream.BufferedData.Length;
            }
            else
              this._ContentLength = this.HttpWriteMode != HttpWriteMode.Chunked ? this._originalContentLength : -1L;
          }
          if (this.HttpWriteMode == HttpWriteMode.Chunked && (this._resendRequestContent == null || this.NtlmKeepAlive))
          {
            this.HttpWriteMode = HttpWriteMode.ContentLength;
            this._SubmitWriteStream.SwitchToContentLength();
            this._HttpRequestHeaders.RemoveInternal("Transfer-Encoding");
            if (this._resendRequestContent != null)
              num1 = -2L;
          }
        }
      }
      return num1;
    }

    private void PostSwitchToContentLength(long value)
    {
      if (value > -1L)
        this._ContentLength = value;
      if (value != -2L)
        return;
      this._ContentLength = -1L;
      this.HttpWriteMode = HttpWriteMode.Chunked;
    }

    private void ClearAuthenticatedConnectionResources()
    {
      if (this.ProxyAuthenticationState.UniqueGroupId != null || this.ServerAuthenticationState.UniqueGroupId != null)
        this.ServicePoint.ReleaseConnectionGroup(this.GetConnectionGroupLine());
      UnlockConnectionDelegate connectionDelegate = this.UnlockConnectionDelegate;
      try
      {
        if (connectionDelegate != null)
          connectionDelegate();
        this.UnlockConnectionDelegate = (UnlockConnectionDelegate) null;
      }
      catch (Exception ex)
      {
        if (NclUtilities.IsFatal(ex))
          throw;
      }
      this.ProxyAuthenticationState.ClearSession(this);
      this.ServerAuthenticationState.ClearSession(this);
    }

    private void CheckProtocol(bool onRequestStream)
    {
      if (!this.CanGetRequestStream)
      {
        if (onRequestStream)
          throw new ProtocolViolationException(SR.GetString("net_nouploadonget"));
        if (this.HttpWriteMode != HttpWriteMode.Unknown && this.HttpWriteMode != HttpWriteMode.None || (this.ContentLength > 0L || this.SendChunked))
          throw new ProtocolViolationException(SR.GetString("net_nocontentlengthonget"));
        this.HttpWriteMode = HttpWriteMode.None;
      }
      else if (this.HttpWriteMode == HttpWriteMode.Unknown)
      {
        if (this.SendChunked)
        {
          if (this.ServicePoint.HttpBehaviour == HttpBehaviour.HTTP11 || this.ServicePoint.HttpBehaviour == HttpBehaviour.Unknown)
          {
            this.HttpWriteMode = HttpWriteMode.Chunked;
          }
          else
          {
            if (!this.AllowWriteStreamBuffering)
              throw new ProtocolViolationException(SR.GetString("net_nochunkuploadonhttp10"));
            this.HttpWriteMode = HttpWriteMode.Buffer;
          }
        }
        else
          this.HttpWriteMode = this.ContentLength >= 0L ? HttpWriteMode.ContentLength : (onRequestStream ? HttpWriteMode.Buffer : HttpWriteMode.None);
      }
      if (this.HttpWriteMode == HttpWriteMode.Chunked)
        return;
      if ((onRequestStream || this._OriginVerb.Equals(KnownHttpVerb.Post) || this._OriginVerb.Equals(KnownHttpVerb.Put)) && (this.ContentLength == -1L && !this.AllowWriteStreamBuffering && this.KeepAlive))
        throw new ProtocolViolationException(SR.GetString("net_contentlengthmissing"));
      if (!ValidationHelper.IsBlankString(this.TransferEncoding))
        throw new InvalidOperationException(SR.GetString("net_needchunked"));
    }

    /// <summary>
    /// Begins an asynchronous request for a <see cref="T:System.IO.Stream"/> object to use to write data.
    /// </summary>
    /// 
    /// <returns>
    /// An <see cref="T:System.IAsyncResult"/> that references the asynchronous request.
    /// </returns>
    /// <param name="callback">The <see cref="T:System.AsyncCallback"/> delegate. </param><param name="state">The state object for this request. </param><exception cref="T:System.Net.ProtocolViolationException">The <see cref="P:System.Net.HttpWebRequest.Method"/> property is GET or HEAD.-or- <see cref="P:System.Net.HttpWebRequest.KeepAlive"/> is true, <see cref="P:System.Net.HttpWebRequest.AllowWriteStreamBuffering"/> is false, <see cref="P:System.Net.HttpWebRequest.ContentLength"/> is -1, <see cref="P:System.Net.HttpWebRequest.SendChunked"/> is false, and <see cref="P:System.Net.HttpWebRequest.Method"/> is POST or PUT. </exception><exception cref="T:System.InvalidOperationException">The stream is being used by a previous call to <see cref="M:System.Net.HttpWebRequest.BeginGetRequestStream(System.AsyncCallback,System.Object)"/>-or- <see cref="P:System.Net.HttpWebRequest.TransferEncoding"/> is set to a value and <see cref="P:System.Net.HttpWebRequest.SendChunked"/> is false.-or- The thread pool is running out of threads. </exception><exception cref="T:System.NotSupportedException">The request cache validator indicated that the response for this request can be served from the cache; however, requests that write data must not use the cache. This exception can occur if you are using a custom cache validator that is incorrectly implemented. </exception><exception cref="T:System.Net.WebException"><see cref="M:System.Net.HttpWebRequest.Abort"/> was previously called. </exception><exception cref="T:System.ObjectDisposedException">In a .NET Compact Framework application, a request stream with zero content length was not obtained and closed correctly. For more information about handling zero content length requests, see Network Programming in the .NET Compact Framework.</exception><PermissionSet><IPermission class="System.Security.Permissions.EnvironmentPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Net.DnsPermission, System, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Net.WebPermission, System, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Diagnostics.PerformanceCounterPermission, System, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/></PermissionSet>
    [__DynamicallyInvokable]
    [HostProtection(SecurityAction.LinkDemand, ExternalThreading = true)]
    public override IAsyncResult BeginGetRequestStream(AsyncCallback callback, object state)
    {
      bool success = false;
      try
      {
        if (Logging.On)
          Logging.Enter(Logging.Web, (object) this, "BeginGetRequestStream", "");
        this.CheckProtocol(true);
        ContextAwareResult contextAwareResult = new ContextAwareResult(this.IdentityRequired, true, (object) this, state, callback);
        object obj = contextAwareResult.StartPostingAsyncOp();
        bool lockTaken1 = false;
        try
        {
          Monitor.Enter(obj, ref lockTaken1);
          if (this._WriteAResult != null && this._WriteAResult.InternalPeekCompleted)
          {
            if (this._WriteAResult.Result is Exception)
              throw (Exception) this._WriteAResult.Result;
            try
            {
              contextAwareResult.InvokeCallback(this._WriteAResult.Result);
            }
            catch (Exception ex)
            {
              this.Abort(ex, 1);
              throw;
            }
          }
          else
          {
            if (!this.RequestSubmitted && NclUtilities.IsThreadPoolLow())
            {
              Exception exception = (Exception) new InvalidOperationException(SR.GetString("net_needmorethreads"));
              this.Abort(exception, 1);
              throw exception;
            }
            HttpWebRequest httpWebRequest = this;
            bool lockTaken2 = false;
            try
            {
              Monitor.Enter((object) httpWebRequest, ref lockTaken2);
              if (this._WriteAResult != null)
                throw new InvalidOperationException(SR.GetString("net_repcall"));
              if (this.SetRequestSubmitted())
                throw new InvalidOperationException(SR.GetString("net_reqsubmitted"));
              if (this._ReadAResult != null)
                throw (Exception) this._ReadAResult.Result;
              this._WriteAResult = (LazyAsyncResult) contextAwareResult;
              this.Async = true;
            }
            finally
            {
              if (lockTaken2)
                Monitor.Exit((object) httpWebRequest);
            }
            this.CurrentMethod = this._OriginVerb;
            this.BeginSubmitRequest();
          }
          contextAwareResult.FinishPostingAsyncOp();
        }
        finally
        {
          if (lockTaken1)
            Monitor.Exit(obj);
        }
        if (Logging.On)
          Logging.Exit(Logging.Web, (object) this, "BeginGetRequestStream", (object) contextAwareResult);
        success = true;
        return (IAsyncResult) contextAwareResult;
      }
      finally
      {
        if (FrameworkEventSource.Log.IsEnabled())
          this.LogBeginGetRequestStream(success, false);
      }
    }

    /// <summary>
    /// Ends an asynchronous request for a <see cref="T:System.IO.Stream"/> object to use to write data.
    /// </summary>
    /// 
    /// <returns>
    /// A <see cref="T:System.IO.Stream"/> to use to write request data.
    /// </returns>
    /// <param name="asyncResult">The pending request for a stream. </param><exception cref="T:System.ArgumentNullException"><paramref name="asyncResult"/> is null. </exception><exception cref="T:System.IO.IOException">The request did not complete, and no stream is available. </exception><exception cref="T:System.ArgumentException"><paramref name="asyncResult"/> was not returned by the current instance from a call to <see cref="M:System.Net.HttpWebRequest.BeginGetRequestStream(System.AsyncCallback,System.Object)"/>. </exception><exception cref="T:System.InvalidOperationException">This method was called previously using <paramref name="asyncResult"/>. </exception><exception cref="T:System.Net.WebException"><see cref="M:System.Net.HttpWebRequest.Abort"/> was previously called.-or- An error occurred while processing the request. </exception><PermissionSet><IPermission class="System.Security.Permissions.EnvironmentPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Flags="UnmanagedCode, ControlEvidence"/></PermissionSet>
    [__DynamicallyInvokable]
    public override Stream EndGetRequestStream(IAsyncResult asyncResult)
    {
      TransportContext context;
      return this.EndGetRequestStream(asyncResult, out context);
    }

    /// <summary>
    /// Ends an asynchronous request for a <see cref="T:System.IO.Stream"/> object to use to write data and outputs the <see cref="T:System.Net.TransportContext"/> associated with the stream.
    /// </summary>
    /// 
    /// <returns>
    /// A <see cref="T:System.IO.Stream"/> to use to write request data.
    /// </returns>
    /// <param name="asyncResult">The pending request for a stream.</param><param name="context">The <see cref="T:System.Net.TransportContext"/> for the <see cref="T:System.IO.Stream"/>.</param><exception cref="T:System.ArgumentException"><paramref name="asyncResult"/> was not returned by the current instance from a call to <see cref="M:System.Net.HttpWebRequest.BeginGetRequestStream(System.AsyncCallback,System.Object)"/>. </exception><exception cref="T:System.ArgumentNullException"><paramref name="asyncResult"/> is null. </exception><exception cref="T:System.InvalidOperationException">This method was called previously using <paramref name="asyncResult"/>. </exception><exception cref="T:System.IO.IOException">The request did not complete, and no stream is available. </exception><exception cref="T:System.Net.WebException"><see cref="M:System.Net.HttpWebRequest.Abort"/> was previously called.-or- An error occurred while processing the request. </exception>
    public Stream EndGetRequestStream(IAsyncResult asyncResult, out TransportContext context)
    {
      bool success = false;
      try
      {
        if (Logging.On)
          Logging.Enter(Logging.Web, (object) this, "EndGetRequestStream", "");
        context = (TransportContext) null;
        if (asyncResult == null)
          throw new ArgumentNullException("asyncResult");
        LazyAsyncResult lazyAsyncResult = asyncResult as LazyAsyncResult;
        if (lazyAsyncResult == null || lazyAsyncResult.AsyncObject != this)
          throw new ArgumentException(SR.GetString("net_io_invalidasyncresult"), "asyncResult");
        if (lazyAsyncResult.EndCalled)
          throw new InvalidOperationException(SR.GetString("net_io_invalidendcall", new object[1]
          {
            (object) "EndGetRequestStream"
          }));
        ConnectStream connectStream = lazyAsyncResult.InternalWaitForCompletion() as ConnectStream;
        lazyAsyncResult.EndCalled = true;
        if (connectStream == null)
        {
          if (Logging.On)
            Logging.Exception(Logging.Web, (object) this, "EndGetRequestStream", lazyAsyncResult.Result as Exception);
          throw (Exception) lazyAsyncResult.Result;
        }
        context = (TransportContext) new ConnectStreamContext(connectStream);
        if (Logging.On)
          Logging.Exit(Logging.Web, (object) this, "EndGetRequestStream", (object) connectStream);
        success = true;
        return (Stream) connectStream;
      }
      finally
      {
        if (FrameworkEventSource.Log.IsEnabled())
          this.LogEndGetRequestStream(success, false);
      }
    }

    /// <summary>
    /// Gets a <see cref="T:System.IO.Stream"/> object to use to write request data.
    /// </summary>
    /// 
    /// <returns>
    /// A <see cref="T:System.IO.Stream"/> to use to write request data.
    /// </returns>
    /// <exception cref="T:System.Net.ProtocolViolationException">The <see cref="P:System.Net.HttpWebRequest.Method"/> property is GET or HEAD.-or- <see cref="P:System.Net.HttpWebRequest.KeepAlive"/> is true, <see cref="P:System.Net.HttpWebRequest.AllowWriteStreamBuffering"/> is false, <see cref="P:System.Net.HttpWebRequest.ContentLength"/> is -1, <see cref="P:System.Net.HttpWebRequest.SendChunked"/> is false, and <see cref="P:System.Net.HttpWebRequest.Method"/> is POST or PUT. </exception><exception cref="T:System.InvalidOperationException">The <see cref="M:System.Net.HttpWebRequest.GetRequestStream"/> method is called more than once.-or- <see cref="P:System.Net.HttpWebRequest.TransferEncoding"/> is set to a value and <see cref="P:System.Net.HttpWebRequest.SendChunked"/> is false. </exception><exception cref="T:System.NotSupportedException">The request cache validator indicated that the response for this request can be served from the cache; however, requests that write data must not use the cache. This exception can occur if you are using a custom cache validator that is incorrectly implemented. </exception><exception cref="T:System.Net.WebException"><see cref="M:System.Net.HttpWebRequest.Abort"/> was previously called.-or- The time-out period for the request expired.-or- An error occurred while processing the request. </exception><exception cref="T:System.ObjectDisposedException">In a .NET Compact Framework application, a request stream with zero content length was not obtained and closed correctly. For more information about handling zero content length requests, see Network Programming in the .NET Compact Framework.</exception><PermissionSet><IPermission class="System.Security.Permissions.EnvironmentPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Net.DnsPermission, System, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Net.WebPermission, System, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Diagnostics.PerformanceCounterPermission, System, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/></PermissionSet>
    public override Stream GetRequestStream()
    {
      TransportContext context;
      return this.GetRequestStream(out context);
    }

    /// <summary>
    /// Gets a <see cref="T:System.IO.Stream"/> object to use to write request data and outputs the <see cref="T:System.Net.TransportContext"/> associated with the stream.
    /// </summary>
    /// 
    /// <returns>
    /// A <see cref="T:System.IO.Stream"/> to use to write request data.
    /// </returns>
    /// <param name="context">The <see cref="T:System.Net.TransportContext"/> for the <see cref="T:System.IO.Stream"/>.</param><exception cref="T:System.Exception">The <see cref="M:System.Net.HttpWebRequest.GetRequestStream"/> method was unable to obtain the <see cref="T:System.IO.Stream"/>.</exception><exception cref="T:System.InvalidOperationException">The <see cref="M:System.Net.HttpWebRequest.GetRequestStream"/> method is called more than once.-or- <see cref="P:System.Net.HttpWebRequest.TransferEncoding"/> is set to a value and <see cref="P:System.Net.HttpWebRequest.SendChunked"/> is false. </exception><exception cref="T:System.NotSupportedException">The request cache validator indicated that the response for this request can be served from the cache; however, requests that write data must not use the cache. This exception can occur if you are using a custom cache validator that is incorrectly implemented. </exception><exception cref="T:System.Net.ProtocolViolationException">The <see cref="P:System.Net.HttpWebRequest.Method"/> property is GET or HEAD.-or- <see cref="P:System.Net.HttpWebRequest.KeepAlive"/> is true, <see cref="P:System.Net.HttpWebRequest.AllowWriteStreamBuffering"/> is false, <see cref="P:System.Net.HttpWebRequest.ContentLength"/> is -1, <see cref="P:System.Net.HttpWebRequest.SendChunked"/> is false, and <see cref="P:System.Net.HttpWebRequest.Method"/> is POST or PUT. </exception><exception cref="T:System.Net.WebException"><see cref="M:System.Net.HttpWebRequest.Abort"/> was previously called.-or- The time-out period for the request expired.-or- An error occurred while processing the request. </exception>
    public Stream GetRequestStream(out TransportContext context)
    {
      bool success = false;
      try
      {
        if (FrameworkEventSource.Log.IsEnabled())
          this.LogBeginGetRequestStream(true, true);
        if (Logging.On)
          Logging.Enter(Logging.Web, (object) this, "GetRequestStream", "");
        context = (TransportContext) null;
        this.CheckProtocol(true);
        if (this._WriteAResult == null || !this._WriteAResult.InternalPeekCompleted)
        {
          HttpWebRequest httpWebRequest = this;
          bool lockTaken = false;
          try
          {
            Monitor.Enter((object) httpWebRequest, ref lockTaken);
            if (this._WriteAResult != null)
              throw new InvalidOperationException(SR.GetString("net_repcall"));
            if (this.SetRequestSubmitted())
              throw new InvalidOperationException(SR.GetString("net_reqsubmitted"));
            if (this._ReadAResult != null)
              throw (Exception) this._ReadAResult.Result;
            this._WriteAResult = new LazyAsyncResult((object) this, (object) null, (AsyncCallback) null);
            this.Async = false;
          }
          finally
          {
            if (lockTaken)
              Monitor.Exit((object) httpWebRequest);
          }
          this.CurrentMethod = this._OriginVerb;
          while (this.m_Retry && !this._WriteAResult.InternalPeekCompleted)
          {
            this._OldSubmitWriteStream = (ConnectStream) null;
            this._SubmitWriteStream = (ConnectStream) null;
            this.BeginSubmitRequest();
          }
          while (this.Aborted && !this._WriteAResult.InternalPeekCompleted)
          {
            if (!(this._CoreResponse is Exception))
              Thread.SpinWait(1);
            else
              this.CheckWriteSideResponseProcessing();
          }
        }
        ConnectStream connectStream = this._WriteAResult.InternalWaitForCompletion() as ConnectStream;
        this._WriteAResult.EndCalled = true;
        success = true;
        if (connectStream == null)
        {
          if (Logging.On)
            Logging.Exception(Logging.Web, (object) this, "EndGetRequestStream", this._WriteAResult.Result as Exception);
          throw (Exception) this._WriteAResult.Result;
        }
        context = (TransportContext) new ConnectStreamContext(connectStream);
        if (Logging.On)
          Logging.Exit(Logging.Web, (object) this, "GetRequestStream", (object) connectStream);
        return (Stream) connectStream;
      }
      finally
      {
        if (FrameworkEventSource.Log.IsEnabled())
          this.LogEndGetRequestStream(success, true);
      }
    }

    internal void ErrorStatusCodeNotify(System.Net.Connection connection, bool isKeepAlive, bool fatal)
    {
      ConnectStream connectStream = this._SubmitWriteStream;
      if (connectStream == null || connectStream.Connection != connection)
        return;
      if (!fatal)
      {
        connectStream.ErrorResponseNotify(isKeepAlive);
      }
      else
      {
        if (this.Aborted)
          return;
        connectStream.FatalResponseNotify();
      }
    }

    private HttpProcessingResult DoSubmitRequestProcessing(ref Exception exception)
    {
      HttpProcessingResult processingResult = HttpProcessingResult.Continue;
      this.m_Retry = false;
      bool ntlmKeepAlive = this.NtlmKeepAlive;
      try
      {
        if (this._HttpResponse != null)
        {
          if (this._CookieContainer != null)
            CookieModule.OnReceivedHeaders(this);
          this.ProxyAuthenticationState.Update(this);
          this.ServerAuthenticationState.Update(this);
        }
        bool flag1 = false;
        bool flag2 = true;
        bool disableUpload = false;
        if (this._HttpResponse == null)
          flag1 = true;
        else if (this.CheckResubmitForCache(ref exception) || this.CheckResubmit(ref exception, ref disableUpload))
        {
          flag1 = true;
          flag2 = false;
        }
        else if (disableUpload)
        {
          flag1 = false;
          flag2 = false;
          processingResult = HttpProcessingResult.WriteWait;
          this._AutoRedirects = this._AutoRedirects - 1;
          this.OpenWriteSideResponseWindow();
          ConnectionReturnResult returnResult = new ConnectionReturnResult(1);
          ConnectionReturnResult.Add(ref returnResult, this, this._HttpResponse.CoreResponseData);
          this.m_PendingReturnResult = (object) returnResult;
          this._HttpResponse = (HttpWebResponse) null;
          this._SubmitWriteStream.FinishedAfterWrite = true;
          this.SetRequestContinue();
        }
        ServicePoint servicePoint = (ServicePoint) null;
        if (flag2)
        {
          WebException webException = exception as WebException;
          if (webException != null && webException.InternalStatus == WebExceptionInternalStatus.ServicePointFatal)
          {
            ProxyChain chain = this._ProxyChain;
            if (chain != null)
              servicePoint = ServicePointManager.FindServicePoint(chain);
            flag1 = servicePoint != null;
          }
        }
        if (flag1)
        {
          if (this.CacheProtocol != null && this._HttpResponse != null)
            this.CacheProtocol.Reset();
          this.ClearRequestForResubmit(ntlmKeepAlive);
          WebException webException = exception as WebException;
          if (webException != null && (webException.Status == WebExceptionStatus.PipelineFailure || webException.Status == WebExceptionStatus.KeepAliveFailure))
            this.m_Extra401Retry = true;
          if (servicePoint == null)
            servicePoint = this.FindServicePoint(true);
          else
            this._ServicePoint = servicePoint;
          if (this.Async)
            this.SubmitRequest(servicePoint);
          else
            this.m_Retry = true;
          processingResult = HttpProcessingResult.WriteWait;
        }
      }
      finally
      {
        if (processingResult == HttpProcessingResult.Continue)
          this.ClearAuthenticatedConnectionResources();
      }
      return processingResult;
    }

    /// <summary>
    /// Begins an asynchronous request to an Internet resource.
    /// </summary>
    /// 
    /// <returns>
    /// An <see cref="T:System.IAsyncResult"/> that references the asynchronous request for a response.
    /// </returns>
    /// <param name="callback">The <see cref="T:System.AsyncCallback"/> delegate </param><param name="state">The state object for this request. </param><exception cref="T:System.InvalidOperationException">The stream is already in use by a previous call to <see cref="M:System.Net.HttpWebRequest.BeginGetResponse(System.AsyncCallback,System.Object)"/>-or- <see cref="P:System.Net.HttpWebRequest.TransferEncoding"/> is set to a value and <see cref="P:System.Net.HttpWebRequest.SendChunked"/> is false.-or- The thread pool is running out of threads. </exception><exception cref="T:System.Net.ProtocolViolationException"><see cref="P:System.Net.HttpWebRequest.Method"/> is GET or HEAD, and either <see cref="P:System.Net.HttpWebRequest.ContentLength"/> is greater than zero or <see cref="P:System.Net.HttpWebRequest.SendChunked"/> is true.-or- <see cref="P:System.Net.HttpWebRequest.KeepAlive"/> is true, <see cref="P:System.Net.HttpWebRequest.AllowWriteStreamBuffering"/> is false, and either <see cref="P:System.Net.HttpWebRequest.ContentLength"/> is -1, <see cref="P:System.Net.HttpWebRequest.SendChunked"/> is false and <see cref="P:System.Net.HttpWebRequest.Method"/> is POST or PUT.-or- The <see cref="T:System.Net.HttpWebRequest"/> has an entity body but the <see cref="M:System.Net.HttpWebRequest.BeginGetResponse(System.AsyncCallback,System.Object)"/> method is called without calling the <see cref="M:System.Net.HttpWebRequest.BeginGetRequestStream(System.AsyncCallback,System.Object)"/> method. -or- The <see cref="P:System.Net.HttpWebRequest.ContentLength"/> is greater than zero, but the application does not write all of the promised data.</exception><exception cref="T:System.Net.WebException"><see cref="M:System.Net.HttpWebRequest.Abort"/> was previously called. </exception><PermissionSet><IPermission class="System.Security.Permissions.EnvironmentPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Net.DnsPermission, System, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Net.WebPermission, System, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Diagnostics.PerformanceCounterPermission, System, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/></PermissionSet>
    [__DynamicallyInvokable]
    [HostProtection(SecurityAction.LinkDemand, ExternalThreading = true)]
    public override IAsyncResult BeginGetResponse(AsyncCallback callback, object state)
    {
      bool success = false;
      try
      {
        if (Logging.On)
          Logging.Enter(Logging.Web, (object) this, "BeginGetResponse", "");
        if (!this.RequestSubmitted)
          this.CheckProtocol(false);
        ConnectStream stream = this._OldSubmitWriteStream != null ? this._OldSubmitWriteStream : this._SubmitWriteStream;
        if (stream != null && !stream.IsClosed)
        {
          if (stream.BytesLeftToWrite > 0L)
            throw new ProtocolViolationException(SR.GetString("net_entire_body_not_written"));
          stream.Close();
        }
        else if (stream == null && this.HasEntityBody)
          throw new ProtocolViolationException(SR.GetString("net_must_provide_request_body"));
        ContextAwareResult contextAwareResult = new ContextAwareResult(this.IdentityRequired, true, (object) this, state, callback);
        if (!this.RequestSubmitted && NclUtilities.IsThreadPoolLow())
        {
          Exception exception = (Exception) new InvalidOperationException(SR.GetString("net_needmorethreads"));
          this.Abort(exception, 1);
          throw exception;
        }
        object obj = contextAwareResult.StartPostingAsyncOp();
        bool lockTaken1 = false;
        try
        {
          Monitor.Enter(obj, ref lockTaken1);
          bool flag1 = false;
          HttpWebRequest httpWebRequest = this;
          bool lockTaken2 = false;
          bool flag2;
          try
          {
            Monitor.Enter((object) httpWebRequest, ref lockTaken2);
            flag2 = this.SetRequestSubmitted();
            if (this.HaveResponse)
            {
              flag1 = true;
            }
            else
            {
              if (this._ReadAResult != null)
                throw new InvalidOperationException(SR.GetString("net_repcall"));
              this._ReadAResult = (LazyAsyncResult) contextAwareResult;
              this.Async = true;
            }
          }
          finally
          {
            if (lockTaken2)
              Monitor.Exit((object) httpWebRequest);
          }
          this.CheckDeferredCallDone(stream);
          if (flag1)
          {
            if (Logging.On)
              Logging.Exit(Logging.Web, (object) this, "BeginGetResponse", this._ReadAResult.Result);
            Exception exception = this._ReadAResult.Result as Exception;
            if (exception != null)
              throw exception;
            try
            {
              contextAwareResult.InvokeCallback(this._ReadAResult.Result);
            }
            catch (Exception ex)
            {
              this.Abort(ex, 1);
              throw;
            }
          }
          else
          {
            if (!flag2)
              this.CurrentMethod = this._OriginVerb;
            if (this._RerequestCount > 0 || !flag2)
            {
              while (this.m_Retry)
                this.BeginSubmitRequest();
            }
          }
          contextAwareResult.FinishPostingAsyncOp();
        }
        finally
        {
          if (lockTaken1)
            Monitor.Exit(obj);
        }
        if (Logging.On)
          Logging.Exit(Logging.Web, (object) this, "BeginGetResponse", (object) contextAwareResult);
        success = true;
        return (IAsyncResult) contextAwareResult;
      }
      finally
      {
        if (FrameworkEventSource.Log.IsEnabled())
          this.LogBeginGetResponse(success, false);
      }
    }

    /// <summary>
    /// Ends an asynchronous request to an Internet resource.
    /// </summary>
    /// 
    /// <returns>
    /// A <see cref="T:System.Net.WebResponse"/> that contains the response from the Internet resource.
    /// </returns>
    /// <param name="asyncResult">The pending request for a response. </param><exception cref="T:System.ArgumentNullException"><paramref name="asyncResult"/> is null. </exception><exception cref="T:System.InvalidOperationException">This method was called previously using <paramref name="asyncResult."/>-or- The <see cref="P:System.Net.HttpWebRequest.ContentLength"/> property is greater than 0 but the data has not been written to the request stream. </exception><exception cref="T:System.Net.WebException"><see cref="M:System.Net.HttpWebRequest.Abort"/> was previously called.-or- An error occurred while processing the request. </exception><exception cref="T:System.ArgumentException"><paramref name="asyncResult"/> was not returned by the current instance from a call to <see cref="M:System.Net.HttpWebRequest.BeginGetResponse(System.AsyncCallback,System.Object)"/>. </exception><PermissionSet><IPermission class="System.Security.Permissions.EnvironmentPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Flags="UnmanagedCode, ControlEvidence"/></PermissionSet>
    [__DynamicallyInvokable]
    public override WebResponse EndGetResponse(IAsyncResult asyncResult)
    {
      bool success = false;
      int statusCode = -1;
      try
      {
        if (Logging.On)
          Logging.Enter(Logging.Web, (object) this, "EndGetResponse", "");
        if (asyncResult == null)
          throw new ArgumentNullException("asyncResult");
        LazyAsyncResult lazyAsyncResult = asyncResult as LazyAsyncResult;
        if (lazyAsyncResult == null || lazyAsyncResult.AsyncObject != this)
          throw new ArgumentException(SR.GetString("net_io_invalidasyncresult"), "asyncResult");
        if (lazyAsyncResult.EndCalled)
          throw new InvalidOperationException(SR.GetString("net_io_invalidendcall", new object[1]
          {
            (object) "EndGetResponse"
          }));
        HttpWebResponse httpWebResponse = lazyAsyncResult.InternalWaitForCompletion() as HttpWebResponse;
        lazyAsyncResult.EndCalled = true;
        if (httpWebResponse == null)
        {
          if (Logging.On)
            Logging.Exception(Logging.Web, (object) this, "EndGetResponse", lazyAsyncResult.Result as Exception);
          NetworkingPerfCounters.Instance.Increment(NetworkingPerfCounterName.HttpWebRequestFailed);
          throw (Exception) lazyAsyncResult.Result;
        }
        if (Logging.On)
          Logging.Exit(Logging.Web, (object) this, "EndGetResponse", (object) httpWebResponse);
        this.InitLifetimeTracking(httpWebResponse);
        statusCode = HttpWebRequest.GetStatusCode(httpWebResponse);
        success = true;
        return (WebResponse) httpWebResponse;
      }
      catch (WebException ex)
      {
        statusCode = HttpWebRequest.GetStatusCode(ex.Response as HttpWebResponse);
        throw;
      }
      finally
      {
        if (FrameworkEventSource.Log.IsEnabled())
          this.LogEndGetResponse(success, false, statusCode);
      }
    }

    private void CheckDeferredCallDone(ConnectStream stream)
    {
      object obj = Interlocked.Exchange(ref this.m_PendingReturnResult, (object) DBNull.Value);
      if (obj == NclConstants.Sentinel)
      {
        this.EndSubmitRequest();
      }
      else
      {
        if (obj == null || obj == DBNull.Value)
          return;
        stream.ProcessWriteCallDone(obj as ConnectionReturnResult);
      }
    }

    /// <summary>
    /// Returns a response from an Internet resource.
    /// </summary>
    /// 
    /// <returns>
    /// A <see cref="T:System.Net.WebResponse"/> that contains the response from the Internet resource.
    /// </returns>
    /// <exception cref="T:System.InvalidOperationException">The stream is already in use by a previous call to <see cref="M:System.Net.HttpWebRequest.BeginGetResponse(System.AsyncCallback,System.Object)"/>.-or- <see cref="P:System.Net.HttpWebRequest.TransferEncoding"/> is set to a value and <see cref="P:System.Net.HttpWebRequest.SendChunked"/> is false. </exception><exception cref="T:System.Net.ProtocolViolationException"><see cref="P:System.Net.HttpWebRequest.Method"/> is GET or HEAD, and either <see cref="P:System.Net.HttpWebRequest.ContentLength"/> is greater or equal to zero or <see cref="P:System.Net.HttpWebRequest.SendChunked"/> is true.-or- <see cref="P:System.Net.HttpWebRequest.KeepAlive"/> is true, <see cref="P:System.Net.HttpWebRequest.AllowWriteStreamBuffering"/> is false, <see cref="P:System.Net.HttpWebRequest.ContentLength"/> is -1, <see cref="P:System.Net.HttpWebRequest.SendChunked"/> is false, and <see cref="P:System.Net.HttpWebRequest.Method"/> is POST or PUT. -or- The <see cref="T:System.Net.HttpWebRequest"/> has an entity body but the <see cref="M:System.Net.HttpWebRequest.GetResponse"/> method is called without calling the <see cref="M:System.Net.HttpWebRequest.GetRequestStream"/> method. -or- The <see cref="P:System.Net.HttpWebRequest.ContentLength"/> is greater than zero, but the application does not write all of the promised data.</exception><exception cref="T:System.NotSupportedException">The request cache validator indicated that the response for this request can be served from the cache; however, this request includes data to be sent to the server. Requests that send data must not use the cache. This exception can occur if you are using a custom cache validator that is incorrectly implemented. </exception><exception cref="T:System.Net.WebException"><see cref="M:System.Net.HttpWebRequest.Abort"/> was previously called.-or- The time-out period for the request expired.-or- An error occurred while processing the request. </exception><PermissionSet><IPermission class="System.Security.Permissions.EnvironmentPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Net.DnsPermission, System, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Net.WebPermission, System, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Diagnostics.PerformanceCounterPermission, System, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/></PermissionSet>
    public override WebResponse GetResponse()
    {
      bool success = false;
      int statusCode = -1;
      try
      {
        if (FrameworkEventSource.Log.IsEnabled())
          this.LogBeginGetResponse(true, true);
        if (Logging.On)
          Logging.Enter(Logging.Web, (object) this, "GetResponse", "");
        if (!this.RequestSubmitted)
          this.CheckProtocol(false);
        ConnectStream stream = this._OldSubmitWriteStream != null ? this._OldSubmitWriteStream : this._SubmitWriteStream;
        if (stream != null && !stream.IsClosed)
        {
          if (stream.BytesLeftToWrite > 0L)
            throw new ProtocolViolationException(SR.GetString("net_entire_body_not_written"));
          stream.Close();
        }
        else if (stream == null && this.HasEntityBody)
          throw new ProtocolViolationException(SR.GetString("net_must_provide_request_body"));
        bool flag1 = false;
        HttpWebResponse httpWebResponse = (HttpWebResponse) null;
        HttpWebRequest httpWebRequest = this;
        bool lockTaken = false;
        bool flag2;
        try
        {
          Monitor.Enter((object) httpWebRequest, ref lockTaken);
          flag2 = this.SetRequestSubmitted();
          if (this.HaveResponse)
          {
            flag1 = true;
            httpWebResponse = this._ReadAResult.Result as HttpWebResponse;
          }
          else
          {
            if (this._ReadAResult != null)
              throw new InvalidOperationException(SR.GetString("net_repcall"));
            this.Async = false;
            if (this.Async)
            {
              ContextAwareResult contextAwareResult = new ContextAwareResult(this.IdentityRequired, true, (object) this, (object) null, (AsyncCallback) null);
              contextAwareResult.StartPostingAsyncOp(false);
              contextAwareResult.FinishPostingAsyncOp();
              this._ReadAResult = (LazyAsyncResult) contextAwareResult;
            }
            else
              this._ReadAResult = new LazyAsyncResult((object) this, (object) null, (AsyncCallback) null);
          }
        }
        finally
        {
          if (lockTaken)
            Monitor.Exit((object) httpWebRequest);
        }
        this.CheckDeferredCallDone(stream);
        if (!flag1)
        {
          if (this._Timer == null)
            this._Timer = this.TimerQueue.CreateTimer(HttpWebRequest.s_TimeoutCallback, (object) this);
          if (!flag2)
            this.CurrentMethod = this._OriginVerb;
          while (this.m_Retry)
            this.BeginSubmitRequest();
          while (!this.Async && this.Aborted && !this._ReadAResult.InternalPeekCompleted)
          {
            if (!(this._CoreResponse is Exception))
              Thread.SpinWait(1);
            else
              this.CheckWriteSideResponseProcessing();
          }
          httpWebResponse = this._ReadAResult.InternalWaitForCompletion() as HttpWebResponse;
          this._ReadAResult.EndCalled = true;
        }
        if (httpWebResponse == null)
        {
          if (Logging.On)
            Logging.Exception(Logging.Web, (object) this, "GetResponse", this._ReadAResult.Result as Exception);
          NetworkingPerfCounters.Instance.Increment(NetworkingPerfCounterName.HttpWebRequestFailed);
          throw (Exception) this._ReadAResult.Result;
        }
        if (Logging.On)
          Logging.Exit(Logging.Web, (object) this, "GetResponse", (object) httpWebResponse);
        if (!flag1)
          this.InitLifetimeTracking(httpWebResponse);
        statusCode = HttpWebRequest.GetStatusCode(httpWebResponse);
        success = true;
        return (WebResponse) httpWebResponse;
      }
      catch (WebException ex)
      {
        statusCode = HttpWebRequest.GetStatusCode(ex.Response as HttpWebResponse);
        throw;
      }
      finally
      {
        if (FrameworkEventSource.Log.IsEnabled())
          this.LogEndGetResponse(success, true, statusCode);
      }
    }

    private void InitLifetimeTracking(HttpWebResponse httpWebResponse)
    {
      (httpWebResponse.ResponseStream as IRequestLifetimeTracker).TrackRequestLifetime(this.m_StartTimestamp);
    }

    internal void WriteCallDone(ConnectStream stream, ConnectionReturnResult returnResult)
    {
      if (stream != (this._OldSubmitWriteStream != null ? this._OldSubmitWriteStream : this._SubmitWriteStream))
        stream.ProcessWriteCallDone(returnResult);
      else if (!this.UserRetrievedWriteStream)
        stream.ProcessWriteCallDone(returnResult);
      else if (stream.FinishedAfterWrite)
      {
        stream.ProcessWriteCallDone(returnResult);
      }
      else
      {
        if (Interlocked.CompareExchange(ref this.m_PendingReturnResult, returnResult == null ? (object) Missing.Value : (object) returnResult, (object) null) != DBNull.Value)
          return;
        stream.ProcessWriteCallDone(returnResult);
      }
    }

    internal void NeedEndSubmitRequest()
    {
      if (Interlocked.CompareExchange(ref this.m_PendingReturnResult, NclConstants.Sentinel, (object) null) != DBNull.Value)
        return;
      this.EndSubmitRequest();
    }

    internal void CallContinueDelegateCallback(object state)
    {
      CoreResponseData coreResponseData = (CoreResponseData) state;
      this.ContinueDelegate((int) coreResponseData.m_StatusCode, coreResponseData.m_ResponseHeaders);
    }

    private DateTime GetDateHeaderHelper(string headerName)
    {
      string S = this._HttpRequestHeaders[headerName];
      if (S == null)
        return DateTime.MinValue;
      return HttpProtocolUtils.string2date(S);
    }

    private void SetDateHeaderHelper(string headerName, DateTime dateTime)
    {
      if (dateTime == DateTime.MinValue)
        this.SetSpecialHeaders(headerName, (string) null);
      else
        this.SetSpecialHeaders(headerName, HttpProtocolUtils.date2string(dateTime));
    }

    internal void FreeWriteBuffer()
    {
      if (this._WriteBufferFromPinnableCache)
      {
        HttpWebRequest._WriteBufferCache.FreeBuffer(this._WriteBuffer);
        this._WriteBufferFromPinnableCache = false;
      }
      this._WriteBufferLength = 0;
      this._WriteBuffer = (byte[]) null;
    }

    private void SetWriteBuffer(int bufferSize)
    {
      if (bufferSize <= 512)
      {
        if (!this._WriteBufferFromPinnableCache)
        {
          this._WriteBuffer = HttpWebRequest._WriteBufferCache.AllocateBuffer();
          this._WriteBufferFromPinnableCache = true;
        }
      }
      else
      {
        this.FreeWriteBuffer();
        this._WriteBuffer = new byte[bufferSize];
      }
      this._WriteBufferLength = bufferSize;
    }

    private void SetSpecialHeaders(string HeaderName, string value)
    {
      value = WebHeaderCollection.CheckBadChars(value, true);
      this._HttpRequestHeaders.RemoveInternal(HeaderName);
      if (value.Length == 0)
        return;
      this._HttpRequestHeaders.AddInternal(HeaderName, value);
    }

    /// <summary>
    /// Cancels a request to an Internet resource.
    /// </summary>
    /// <PermissionSet><IPermission class="System.Security.Permissions.EnvironmentPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Unrestricted="true"/><IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Flags="UnmanagedCode, ControlEvidence"/></PermissionSet>
    [__DynamicallyInvokable]
    public override void Abort()
    {
      this.Abort((Exception) null, 1);
    }

    private void Abort(Exception exception, int abortState)
    {
      if (Logging.On)
        Logging.Enter(Logging.Web, (object) this, "Abort", exception == null ? "" : exception.Message);
      if (Interlocked.CompareExchange(ref this.m_Aborted, abortState, 0) == 0)
      {
        NetworkingPerfCounters.Instance.Increment(NetworkingPerfCounterName.HttpWebRequestAborted);
        this.m_OnceFailed = true;
        this.CancelTimer();
        WebException webException = exception as WebException;
        if (exception == null)
          webException = new WebException(NetRes.GetWebStatusString("net_requestaborted", WebExceptionStatus.RequestCanceled), WebExceptionStatus.RequestCanceled);
        else if (webException == null)
          webException = new WebException(NetRes.GetWebStatusString("net_requestaborted", WebExceptionStatus.RequestCanceled), exception, WebExceptionStatus.RequestCanceled, (WebResponse) this._HttpResponse);
        try
        {
          Thread.MemoryBarrier();
          HttpAbortDelegate httpAbortDelegate = this._AbortDelegate;
          if (httpAbortDelegate == null || httpAbortDelegate(this, webException))
          {
            this.SetResponse((Exception) webException);
          }
          else
          {
            LazyAsyncResult lazyAsyncResult1 = (LazyAsyncResult) null;
            LazyAsyncResult lazyAsyncResult2 = (LazyAsyncResult) null;
            if (!this.Async)
            {
              HttpWebRequest httpWebRequest = this;
              bool lockTaken = false;
              try
              {
                Monitor.Enter((object) httpWebRequest, ref lockTaken);
                lazyAsyncResult1 = this._WriteAResult;
                lazyAsyncResult2 = this._ReadAResult;
              }
              finally
              {
                if (lockTaken)
                  Monitor.Exit((object) httpWebRequest);
              }
            }
            if (lazyAsyncResult1 != null)
              lazyAsyncResult1.InvokeCallback((object) webException);
            if (lazyAsyncResult2 != null)
              lazyAsyncResult2.InvokeCallback((object) webException);
          }
          if (!this.Async)
          {
            LazyAsyncResult connectionAsyncResult = this.ConnectionAsyncResult;
            LazyAsyncResult readerAsyncResult = this.ConnectionReaderAsyncResult;
            if (connectionAsyncResult != null)
              connectionAsyncResult.InvokeCallback((object) webException);
            if (readerAsyncResult != null)
              readerAsyncResult.InvokeCallback((object) webException);
          }
          if (this.IsWebSocketRequest)
          {
            if (this.ServicePoint != null)
              this.ServicePoint.CloseConnectionGroup(this.ConnectionGroupName);
          }
        }
        catch (InternalException ex)
        {
        }
      }
      if (!Logging.On)
        return;
      Logging.Exit(Logging.Web, (object) this, "Abort", "");
    }

    private void CancelTimer()
    {
      TimerThread.Timer timer = this._Timer;
      if (timer == null)
        return;
      timer.Cancel();
    }

    private static void TimeoutCallback(TimerThread.Timer timer, int timeNoticed, object context)
    {
      ThreadPool.UnsafeQueueUserWorkItem(HttpWebRequest.s_AbortWrapper, context);
    }

    private static void AbortWrapper(object context)
    {
      ((HttpWebRequest) context).Abort((Exception) new WebException(NetRes.GetWebStatusString(WebExceptionStatus.Timeout), WebExceptionStatus.Timeout), 1);
    }

    private ServicePoint FindServicePoint(bool forceFind)
    {
      ServicePoint servicePoint = this._ServicePoint;
      if (servicePoint == null | forceFind)
      {
        HttpWebRequest httpWebRequest = this;
        bool lockTaken = false;
        try
        {
          Monitor.Enter((object) httpWebRequest, ref lockTaken);
          if (this._ServicePoint == null | forceFind)
          {
            if (!this.ProxySet)
              this._Proxy = WebRequest.InternalDefaultWebProxy;
            if (this._ProxyChain != null)
              this._ProxyChain.Dispose();
            this._ServicePoint = ServicePointManager.FindServicePoint(this._Uri, this._Proxy, out this._ProxyChain, ref this._AbortDelegate, ref this.m_Aborted);
            if (Logging.On)
              Logging.Associate(Logging.Web, (object) this, (object) this._ServicePoint);
          }
        }
        finally
        {
          if (lockTaken)
            Monitor.Exit((object) httpWebRequest);
        }
        servicePoint = this._ServicePoint;
      }
      return servicePoint;
    }

    private void InvokeGetRequestStreamCallback()
    {
      LazyAsyncResult lazyAsyncResult = this._WriteAResult;
      if (lazyAsyncResult == null)
        return;
      try
      {
        lazyAsyncResult.InvokeCallback((object) this._SubmitWriteStream);
      }
      catch (Exception ex)
      {
        if (NclUtilities.IsFatal(ex))
        {
          throw;
        }
        else
        {
          this.Abort(ex, 1);
          throw;
        }
      }
    }

    internal void SetRequestSubmitDone(ConnectStream submitStream)
    {
      if (!this.Async)
        this.ConnectionAsyncResult.InvokeCallback();
      if (this.AllowWriteStreamBuffering)
        submitStream.EnableWriteBuffering();
      if (submitStream.CanTimeout)
      {
        submitStream.ReadTimeout = this.ReadWriteTimeout;
        submitStream.WriteTimeout = this.ReadWriteTimeout;
      }
      if (Logging.On)
        Logging.Associate(Logging.Web, (object) this, (object) submitStream);
      TransportContext transportContext = (TransportContext) new ConnectStreamContext(submitStream);
      this.ServerAuthenticationState.TransportContext = transportContext;
      this.ProxyAuthenticationState.TransportContext = transportContext;
      this._SubmitWriteStream = submitStream;
      if (this.RtcState != null && this.RtcState.inputData != null && !this.RtcState.IsAborted)
      {
        this.RtcState.outputData = new byte[4];
        this.RtcState.result = this._SubmitWriteStream.SetRtcOption(this.RtcState.inputData, this.RtcState.outputData);
        if (!this.RtcState.IsEnabled())
          this.Abort((Exception) null, 1);
        this.RtcState.connectComplete.Set();
      }
      if (this.Async && this._CoreResponse != null && this._CoreResponse != DBNull.Value)
        submitStream.CallDone();
      else
        this.EndSubmitRequest();
    }

    internal void WriteHeadersCallback(WebExceptionStatus errorStatus, ConnectStream stream, bool async)
    {
      if (errorStatus != WebExceptionStatus.Success)
        return;
      if (!this.EndWriteHeaders(async))
      {
        errorStatus = WebExceptionStatus.Pending;
      }
      else
      {
        if (stream.BytesLeftToWrite != 0L)
          return;
        stream.CallDone();
      }
    }

    internal void SetRequestContinue()
    {
      this.SetRequestContinue((CoreResponseData) null);
    }

    internal void SetRequestContinue(CoreResponseData continueResponse)
    {
      this._RequestContinueCount = this._RequestContinueCount + 1;
      if (this.HttpWriteMode == HttpWriteMode.None || !this.m_ContinueGate.Complete())
        return;
      if (continueResponse != null && this.ContinueDelegate != null)
      {
        ExecutionContext executionContext = this.Async ? this.GetWritingContext().ContextCopy : (ExecutionContext) null;
        if (executionContext == null)
          this.ContinueDelegate((int) continueResponse.m_StatusCode, continueResponse.m_ResponseHeaders);
        else
          ExecutionContext.Run(executionContext, new ContextCallback(this.CallContinueDelegateCallback), (object) continueResponse);
      }
      this.EndWriteHeaders_Part2();
    }

    internal void OpenWriteSideResponseWindow()
    {
      this._CoreResponse = (object) DBNull.Value;
      this._NestedWriteSideCheck = 0;
    }

    internal void CheckWriteSideResponseProcessing()
    {
      object obj = this.Async ? Interlocked.CompareExchange(ref this._CoreResponse, (object) null, (object) DBNull.Value) : this._CoreResponse;
      if (obj == DBNull.Value || obj == null)
        return;
      if (!this.Async)
      {
        int num = this._NestedWriteSideCheck + 1;
        this._NestedWriteSideCheck = num;
        if (num != 1)
          return;
      }
      this.FinishContinueWait();
      Exception E = obj as Exception;
      if (E != null)
        this.SetResponse(E);
      else
        this.SetResponse(obj as CoreResponseData);
    }

    internal void SetAndOrProcessResponse(object responseOrException)
    {
      if (responseOrException == null)
        throw new InternalException();
      CoreResponseData coreResponseData = responseOrException as CoreResponseData;
      WebException webException = responseOrException as WebException;
      object obj = Interlocked.CompareExchange(ref this._CoreResponse, responseOrException, (object) DBNull.Value);
      if (obj != null)
      {
        if (obj.GetType() == typeof (CoreResponseData))
        {
          if (coreResponseData != null)
            throw new InternalException();
          if (webException != null && webException.InternalStatus != WebExceptionInternalStatus.ServicePointFatal && webException.InternalStatus != WebExceptionInternalStatus.RequestFatal)
            return;
        }
        else if (obj.GetType() != typeof (DBNull))
        {
          if (coreResponseData == null)
            throw new InternalException();
          ICloseEx closeEx = coreResponseData.m_ConnectStream as ICloseEx;
          if (closeEx != null)
          {
            closeEx.CloseEx(CloseExState.Silent);
            return;
          }
          coreResponseData.m_ConnectStream.Close();
          return;
        }
      }
      if (obj == DBNull.Value)
      {
        if (!this.Async)
        {
          LazyAsyncResult connectionAsyncResult = this.ConnectionAsyncResult;
          LazyAsyncResult readerAsyncResult = this.ConnectionReaderAsyncResult;
          connectionAsyncResult.InvokeCallback(responseOrException);
          object result = responseOrException;
          readerAsyncResult.InvokeCallback(result);
        }
        else
        {
          if (this.AllowWriteStreamBuffering || !this.IsOutstandingGetRequestStream || !this.FinishContinueWait())
            return;
          if (coreResponseData != null)
            this.SetResponse(coreResponseData);
          else
            this.SetResponse((Exception) webException);
        }
      }
      else if (obj != null)
      {
        Exception E = responseOrException as Exception;
        if (E == null)
          throw new InternalException();
        this.SetResponse(E);
      }
      else if (Interlocked.CompareExchange(ref this._CoreResponse, responseOrException, (object) null) != null && coreResponseData != null)
      {
        ICloseEx closeEx = coreResponseData.m_ConnectStream as ICloseEx;
        if (closeEx != null)
          closeEx.CloseEx(CloseExState.Silent);
        else
          coreResponseData.m_ConnectStream.Close();
      }
      else
      {
        if (!this.Async)
          throw new InternalException();
        this.FinishContinueWait();
        if (coreResponseData != null)
          this.SetResponse(coreResponseData);
        else
          this.SetResponse(responseOrException as Exception);
      }
    }

    private void SetResponse(CoreResponseData coreResponseData)
    {
      try
      {
        if (!this.Async)
        {
          LazyAsyncResult connectionAsyncResult = this.ConnectionAsyncResult;
          LazyAsyncResult readerAsyncResult = this.ConnectionReaderAsyncResult;
          CoreResponseData coreResponseData1 = coreResponseData;
          connectionAsyncResult.InvokeCallback((object) coreResponseData1);
          readerAsyncResult.InvokeCallback((object) coreResponseData);
        }
        if (coreResponseData != null)
        {
          if (coreResponseData.m_ConnectStream.CanTimeout)
          {
            coreResponseData.m_ConnectStream.WriteTimeout = this.ReadWriteTimeout;
            coreResponseData.m_ConnectStream.ReadTimeout = this.ReadWriteTimeout;
          }
          this._HttpResponse = new HttpWebResponse(this.GetRemoteResourceUri(), this.CurrentMethod, coreResponseData, this._MediaType, this.UsesProxySemantics, this.AutomaticDecompression, this.IsWebSocketRequest, this.ConnectionGroupName);
          if (Logging.On)
            Logging.Associate(Logging.Web, (object) this, (object) coreResponseData.m_ConnectStream);
          if (Logging.On)
            Logging.Associate(Logging.Web, (object) this, (object) this._HttpResponse);
          this.ProcessResponse();
        }
        else
          this.Abort((Exception) null, 1);
      }
      catch (Exception ex)
      {
        this.Abort(ex, 2);
      }
    }

    private void ProcessResponse()
    {
      Exception exception = (Exception) null;
      if (this.DoSubmitRequestProcessing(ref exception) != HttpProcessingResult.Continue)
        return;
      this.CancelTimer();
      object result = exception != null ? (object) exception : (object) this._HttpResponse;
      if (this._ReadAResult == null)
      {
        HttpWebRequest httpWebRequest = this;
        bool lockTaken = false;
        try
        {
          Monitor.Enter((object) httpWebRequest, ref lockTaken);
          if (this._ReadAResult == null)
            this._ReadAResult = new LazyAsyncResult((object) null, (object) null, (AsyncCallback) null);
        }
        finally
        {
          if (lockTaken)
            Monitor.Exit((object) httpWebRequest);
        }
      }
      try
      {
        this.FinishRequest(this._HttpResponse, exception);
        this._ReadAResult.InvokeCallback(result);
        try
        {
          this.SetRequestContinue();
        }
        catch
        {
        }
      }
      catch (Exception ex)
      {
        this.Abort(ex, 1);
        throw;
      }
      finally
      {
        if (exception == null && this._ReadAResult.Result != this._HttpResponse)
        {
          WebException webException = this._ReadAResult.Result as WebException;
          if (webException != null && webException.Response != null)
            this._HttpResponse.Abort();
        }
      }
    }

    private void SetResponse(Exception E)
    {
      HttpProcessingResult processingResult = HttpProcessingResult.Continue;
      WebException webException1 = this.HaveResponse ? this._ReadAResult.Result as WebException : (WebException) null;
      WebException webException2 = E as WebException;
      if (webException1 != null && (webException1.InternalStatus == WebExceptionInternalStatus.RequestFatal || webException1.InternalStatus == WebExceptionInternalStatus.ServicePointFatal) && (webException2 == null || webException2.InternalStatus != WebExceptionInternalStatus.RequestFatal))
        E = (Exception) webException1;
      else
        webException1 = webException2;
      if (E != null && Logging.On)
        Logging.Exception(Logging.Web, (object) this, "", (Exception) webException1);
      try
      {
        if (webException1 == null || webException1.InternalStatus != WebExceptionInternalStatus.Isolated && webException1.InternalStatus != WebExceptionInternalStatus.ServicePointFatal && (webException1.InternalStatus != WebExceptionInternalStatus.Recoverable || this.m_OnceFailed))
          return;
        if (webException1.InternalStatus == WebExceptionInternalStatus.Recoverable)
          this.m_OnceFailed = true;
        this.Pipelined = false;
        if (this._SubmitWriteStream != null && this._OldSubmitWriteStream == null && this._SubmitWriteStream.BufferOnly)
          this._OldSubmitWriteStream = this._SubmitWriteStream;
        processingResult = this.DoSubmitRequestProcessing(ref E);
      }
      catch (Exception ex)
      {
        if (NclUtilities.IsFatal(ex))
        {
          throw;
        }
        else
        {
          processingResult = HttpProcessingResult.Continue;
          E = (Exception) new WebException(NetRes.GetWebStatusString("net_requestaborted", WebExceptionStatus.RequestCanceled), ex, WebExceptionStatus.RequestCanceled, (WebResponse) this._HttpResponse);
        }
      }
      finally
      {
        if (processingResult == HttpProcessingResult.Continue)
        {
          this.CancelTimer();
          if (!(E is WebException) && !(E is SecurityException))
          {
            if (this._HttpResponse == null)
              E = (Exception) new WebException(E.Message, E);
            else
              E = (Exception) new WebException(SR.GetString("net_servererror", new object[1]
              {
                (object) NetRes.GetWebStatusCodeString(this.ResponseStatusCode, this._HttpResponse.StatusDescription)
              }), E, WebExceptionStatus.ProtocolError, (WebResponse) this._HttpResponse);
          }
          LazyAsyncResult lazyAsyncResult1 = (LazyAsyncResult) null;
          HttpWebResponse response = this._HttpResponse;
          HttpWebRequest httpWebRequest = this;
          bool lockTaken = false;
          LazyAsyncResult lazyAsyncResult2;
          try
          {
            Monitor.Enter((object) httpWebRequest, ref lockTaken);
            lazyAsyncResult2 = this._WriteAResult;
            if (this._ReadAResult == null)
              this._ReadAResult = new LazyAsyncResult((object) null, (object) null, (AsyncCallback) null, (object) E);
            else
              lazyAsyncResult1 = this._ReadAResult;
          }
          finally
          {
            if (lockTaken)
              Monitor.Exit((object) httpWebRequest);
          }
          try
          {
            this.FinishRequest(response, E);
            try
            {
              if (lazyAsyncResult2 != null)
                lazyAsyncResult2.InvokeCallback((object) E);
            }
            finally
            {
              if (lazyAsyncResult1 != null)
                lazyAsyncResult1.InvokeCallback((object) E);
            }
          }
          finally
          {
            HttpWebResponse httpWebResponse = this._ReadAResult.Result as HttpWebResponse;
            if (httpWebResponse != null)
              httpWebResponse.Abort();
            if (this.CacheProtocol != null)
              this.CacheProtocol.Abort();
          }
        }
      }
    }

    internal override ContextAwareResult GetConnectingContext()
    {
      if (!this.Async)
        return (ContextAwareResult) null;
      ContextAwareResult contextAwareResult = (this.HttpWriteMode == HttpWriteMode.None || this._OldSubmitWriteStream != null || (this._WriteAResult == null || this._WriteAResult.IsCompleted) ? this._ReadAResult : this._WriteAResult) as ContextAwareResult;
      if (contextAwareResult != null)
        return contextAwareResult;
      throw new InternalException();
    }

    internal override ContextAwareResult GetWritingContext()
    {
      if (!this.Async)
        return (ContextAwareResult) null;
      ContextAwareResult contextAwareResult = this._WriteAResult as ContextAwareResult;
      if (contextAwareResult == null || contextAwareResult.InternalPeekCompleted || (this.HttpWriteMode == HttpWriteMode.None || this.HttpWriteMode == HttpWriteMode.Buffer) || (this.m_PendingReturnResult == DBNull.Value || this.m_OriginallyBuffered))
        contextAwareResult = this._ReadAResult as ContextAwareResult;
      if (contextAwareResult == null)
        throw new InternalException();
      return contextAwareResult;
    }

    internal override ContextAwareResult GetReadingContext()
    {
      if (!this.Async)
        return (ContextAwareResult) null;
      ContextAwareResult contextAwareResult = this._ReadAResult as ContextAwareResult;
      if (contextAwareResult == null)
      {
        contextAwareResult = this._WriteAResult as ContextAwareResult;
        if (contextAwareResult == null)
          throw new InternalException();
      }
      return contextAwareResult;
    }

    private void BeginSubmitRequest()
    {
      this.SubmitRequest(this.FindServicePoint(false));
    }

    private void SubmitRequest(ServicePoint servicePoint)
    {
      if (!this.Async)
      {
        this._ConnectionAResult = new LazyAsyncResult((object) this, (object) null, (AsyncCallback) null);
        this._ConnectionReaderAResult = new LazyAsyncResult((object) this, (object) null, (AsyncCallback) null);
        this.OpenWriteSideResponseWindow();
      }
      if (this._Timer == null && !this.Async)
        this._Timer = this.TimerQueue.CreateTimer(HttpWebRequest.s_TimeoutCallback, (object) this);
      try
      {
        if (this._SubmitWriteStream != null && this._SubmitWriteStream.IsPostStream)
        {
          if (this._OldSubmitWriteStream == null && !this._SubmitWriteStream.ErrorInStream && this.AllowWriteStreamBuffering)
            this._OldSubmitWriteStream = this._SubmitWriteStream;
          this._WriteBufferLength = 0;
        }
        this.m_Retry = false;
        if (this.PreAuthenticate)
        {
          if (this.UsesProxySemantics && this._Proxy != null && this._Proxy.Credentials != null)
            this.ProxyAuthenticationState.PreAuthIfNeeded(this, this._Proxy.Credentials);
          if (this.Credentials != null)
            this.ServerAuthenticationState.PreAuthIfNeeded(this, this.Credentials);
        }
        if (this.WriteBufferLength == 0)
          this.UpdateHeaders();
        if (this.CheckCacheRetrieveBeforeSubmit())
          return;
        servicePoint.SubmitRequest(this, this.GetConnectionGroupLine());
      }
      finally
      {
        if (!this.Async)
          this.CheckWriteSideResponseProcessing();
      }
    }

    private bool CheckCacheRetrieveBeforeSubmit()
    {
      if (this.CacheProtocol == null)
        return false;
      try
      {
        Uri cacheUri = this.GetRemoteResourceUri();
        if (cacheUri.Fragment.Length != 0)
          cacheUri = new Uri(cacheUri.GetParts(UriComponents.HttpRequestUrl | UriComponents.UserInfo, UriFormat.SafeUnescaped));
        int num = (int) this.CacheProtocol.GetRetrieveStatus(cacheUri, (WebRequest) this);
        if (this.CacheProtocol.ProtocolStatus == CacheValidationStatus.Fail)
          throw this.CacheProtocol.ProtocolException;
        if (this.CacheProtocol.ProtocolStatus != CacheValidationStatus.ReturnCachedResponse)
          return false;
        if (this.HttpWriteMode != HttpWriteMode.None)
          throw new NotSupportedException(SR.GetString("net_cache_not_supported_body"));
        HttpRequestCacheValidator requestCacheValidator = (HttpRequestCacheValidator) this.CacheProtocol.Validator;
        this._HttpResponse = new HttpWebResponse(this.GetRemoteResourceUri(), this.CurrentMethod, new CoreResponseData()
        {
          m_IsVersionHttp11 = requestCacheValidator.CacheHttpVersion.Equals(HttpVersion.Version11),
          m_StatusCode = requestCacheValidator.CacheStatusCode,
          m_StatusDescription = requestCacheValidator.CacheStatusDescription,
          m_ResponseHeaders = requestCacheValidator.CacheHeaders,
          m_ContentLength = this.CacheProtocol.ResponseStreamLength,
          m_ConnectStream = this.CacheProtocol.ResponseStream
        }, this._MediaType, this.UsesProxySemantics, this.AutomaticDecompression, this.IsWebSocketRequest, this.ConnectionGroupName);
        this._HttpResponse.InternalSetFromCache = true;
        this._HttpResponse.InternalSetIsCacheFresh = requestCacheValidator.CacheFreshnessStatus != CacheFreshnessStatus.Stale;
        this.ProcessResponse();
        return true;
      }
      catch (Exception ex)
      {
        this.Abort(ex, 1);
        throw;
      }
    }

    private bool CheckCacheRetrieveOnResponse()
    {
      if (this.CacheProtocol == null)
        return true;
      if (this.CacheProtocol.ProtocolStatus == CacheValidationStatus.Fail)
        throw this.CacheProtocol.ProtocolException;
      Stream responseStream = this._HttpResponse.ResponseStream;
      int num = (int) this.CacheProtocol.GetRevalidateStatus((WebResponse) this._HttpResponse, this._HttpResponse.ResponseStream);
      if (this.CacheProtocol.ProtocolStatus == CacheValidationStatus.RetryResponseFromServer)
        return false;
      if (this.CacheProtocol.ProtocolStatus != CacheValidationStatus.ReturnCachedResponse && this.CacheProtocol.ProtocolStatus != CacheValidationStatus.CombineCachedAndServerResponse)
        return true;
      if (this.HttpWriteMode != HttpWriteMode.None)
        throw new NotSupportedException(SR.GetString("net_cache_not_supported_body"));
      CoreResponseData coreData = new CoreResponseData();
      HttpRequestCacheValidator requestCacheValidator = (HttpRequestCacheValidator) this.CacheProtocol.Validator;
      coreData.m_IsVersionHttp11 = requestCacheValidator.CacheHttpVersion.Equals(HttpVersion.Version11);
      coreData.m_StatusCode = requestCacheValidator.CacheStatusCode;
      coreData.m_StatusDescription = requestCacheValidator.CacheStatusDescription;
      coreData.m_ResponseHeaders = this.CacheProtocol.ProtocolStatus == CacheValidationStatus.CombineCachedAndServerResponse ? new WebHeaderCollection((NameValueCollection) requestCacheValidator.CacheHeaders) : requestCacheValidator.CacheHeaders;
      coreData.m_ContentLength = this.CacheProtocol.ResponseStreamLength;
      coreData.m_ConnectStream = this.CacheProtocol.ResponseStream;
      this._HttpResponse = new HttpWebResponse(this.GetRemoteResourceUri(), this.CurrentMethod, coreData, this._MediaType, this.UsesProxySemantics, this.AutomaticDecompression, this.IsWebSocketRequest, this.ConnectionGroupName);
      if (this.CacheProtocol.ProtocolStatus == CacheValidationStatus.ReturnCachedResponse)
      {
        this._HttpResponse.InternalSetFromCache = true;
        this._HttpResponse.InternalSetIsCacheFresh = this.CacheProtocol.IsCacheFresh;
        if (responseStream != null)
        {
          try
          {
            responseStream.Close();
          }
          catch
          {
          }
        }
      }
      return true;
    }

    private void CheckCacheUpdateOnResponse()
    {
      if (this.CacheProtocol == null)
        return;
      if (this.CacheProtocol.GetUpdateStatus((WebResponse) this._HttpResponse, this._HttpResponse.ResponseStream) == CacheValidationStatus.UpdateResponseInformation)
        this._HttpResponse.ResponseStream = this.CacheProtocol.ResponseStream;
      else if (this.CacheProtocol.ProtocolStatus == CacheValidationStatus.Fail)
        throw this.CacheProtocol.ProtocolException;
    }

    private void EndSubmitRequest()
    {
      try
      {
        if (this.HttpWriteMode == HttpWriteMode.Buffer)
        {
          this.InvokeGetRequestStreamCallback();
        }
        else
        {
          if (this.WriteBufferLength == 0)
          {
            long num = this.SwitchToContentLength();
            this.SerializeHeaders();
            this.PostSwitchToContentLength(num);
          }
          this._SubmitWriteStream.WriteHeaders(this.Async);
        }
      }
      catch
      {
        ConnectStream connectStream = this._SubmitWriteStream;
        if (connectStream != null)
          connectStream.CallDone();
        throw;
      }
      finally
      {
        if (!this.Async)
          this.CheckWriteSideResponseProcessing();
      }
    }

    internal bool EndWriteHeaders(bool async)
    {
      try
      {
        if (this.ShouldWaitFor100Continue())
          return !async;
        if (this.FinishContinueWait())
        {
          if (this.CompleteContinueGate())
            this.EndWriteHeaders_Part2();
        }
      }
      catch
      {
        ConnectStream connectStream = this._SubmitWriteStream;
        if (connectStream != null)
          connectStream.CallDone();
        throw;
      }
      return true;
    }

    internal bool ShouldWaitFor100Continue()
    {
      if ((this.ContentLength > 0L || this.HttpWriteMode == HttpWriteMode.Chunked) && this.ExpectContinue)
        return this._ServicePoint.Understands100Continue;
      return false;
    }

    private static void ContinueTimeoutCallback(TimerThread.Timer timer, int timeNoticed, object context)
    {
      HttpWebRequest httpWebRequest = (HttpWebRequest) context;
      if (httpWebRequest.HttpWriteMode == HttpWriteMode.None || !httpWebRequest.FinishContinueWait() || !httpWebRequest.CompleteContinueGate())
        return;
      ThreadPool.UnsafeQueueUserWorkItem(HttpWebRequest.s_EndWriteHeaders_Part2Callback, (object) httpWebRequest);
    }

    internal void StartContinueWait()
    {
      this.m_ContinueGate.Trigger(true);
    }

    internal void StartAsync100ContinueTimer()
    {
      if (!this.m_ContinueGate.StartTriggering(true))
        return;
      try
      {
        if (!this.ShouldWaitFor100Continue())
          return;
        this.m_ContinueTimer = this.ContinueTimerQueue.CreateTimer(HttpWebRequest.s_ContinueTimeoutCallback, (object) this);
      }
      finally
      {
        this.m_ContinueGate.FinishTriggering();
      }
    }

    internal bool FinishContinueWait()
    {
      if (!this.m_ContinueGate.StartSignaling(false))
        return false;
      try
      {
        TimerThread.Timer timer = this.m_ContinueTimer;
        this.m_ContinueTimer = (TimerThread.Timer) null;
        if (timer != null)
          timer.Cancel();
      }
      finally
      {
        this.m_ContinueGate.FinishSignaling();
      }
      return true;
    }

    private bool CompleteContinueGate()
    {
      return this.m_ContinueGate.Complete();
    }

    private static void EndWriteHeaders_Part2Wrapper(object state)
    {
      ((HttpWebRequest) state).EndWriteHeaders_Part2();
    }

    internal void EndWriteHeaders_Part2()
    {
      try
      {
        ConnectStream connectStream = this._SubmitWriteStream;
        if (this.HttpWriteMode != HttpWriteMode.None)
        {
          this.m_BodyStarted = true;
          if (this.AllowWriteStreamBuffering || this._resendRequestContent != null)
          {
            if (connectStream.BufferOnly)
              this._OldSubmitWriteStream = connectStream;
            if (this._OldSubmitWriteStream != null || this.UserRetrievedWriteStream && this._resendRequestContent != null)
            {
              if (this._resendRequestContent == null)
                connectStream.ResubmitWrite(this._OldSubmitWriteStream, this.NtlmKeepAlive && this.ContentLength == 0L);
              else if (this.NtlmKeepAlive && (this.ContentLength == 0L || this.HttpWriteMode == HttpWriteMode.Chunked))
              {
                if (this.ContentLength == 0L)
                  connectStream.BytesLeftToWrite = 0L;
              }
              else
              {
                if (this.HttpWriteMode != HttpWriteMode.Chunked)
                  connectStream.BytesLeftToWrite = this._originalContentLength;
                try
                {
                  this._resendRequestContent((Stream) connectStream);
                }
                catch (Exception ex)
                {
                  this.Abort(ex, 1);
                }
              }
              connectStream.CloseInternal(true);
            }
          }
        }
        else
        {
          if (connectStream != null)
            connectStream.CloseInternal(true);
          this._OldSubmitWriteStream = (ConnectStream) null;
        }
        this.InvokeGetRequestStreamCallback();
      }
      catch
      {
        ConnectStream connectStream = this._SubmitWriteStream;
        if (connectStream != null)
          connectStream.CallDone();
        throw;
      }
    }

    private int GenerateConnectRequestLine(int headersSize)
    {
      HostHeaderString hostHeaderString = new HostHeaderString(this.GetSafeHostAndPort(true, true));
      this.SetWriteBuffer(this.CurrentMethod.Name.Length + hostHeaderString.ByteCount + 12 + headersSize);
      int bytes = Encoding.ASCII.GetBytes(this.CurrentMethod.Name, 0, this.CurrentMethod.Name.Length, this.WriteBuffer, 0);
      byte[] writeBuffer1 = this.WriteBuffer;
      int index1 = bytes;
      int num1 = 1;
      int destByteIndex = index1 + num1;
      int num2 = 32;
      writeBuffer1[index1] = (byte) num2;
      hostHeaderString.Copy(this.WriteBuffer, destByteIndex);
      int num3 = destByteIndex + hostHeaderString.ByteCount;
      byte[] writeBuffer2 = this.WriteBuffer;
      int index2 = num3;
      int num4 = 1;
      int num5 = index2 + num4;
      int num6 = 32;
      writeBuffer2[index2] = (byte) num6;
      return num5;
    }

    private string GetSafeHostAndPort(bool addDefaultPort, bool forcePunycode)
    {
      if (this.IsTunnelRequest)
        return HttpWebRequest.GetSafeHostAndPort(this._OriginUri, addDefaultPort, forcePunycode);
      return HttpWebRequest.GetSafeHostAndPort(this._Uri, addDefaultPort, forcePunycode);
    }

    private static string GetSafeHostAndPort(Uri sourceUri, bool addDefaultPort, bool forcePunycode)
    {
      return HttpWebRequest.GetHostAndPortString(sourceUri.HostNameType != UriHostNameType.IPv6 ? (forcePunycode ? sourceUri.IdnHost : sourceUri.DnsSafeHost) : "[" + HttpWebRequest.TrimScopeID(sourceUri.DnsSafeHost) + "]", sourceUri.Port, addDefaultPort || !sourceUri.IsDefaultPort);
    }

    private static string GetHostAndPortString(string hostName, int port, bool addPort)
    {
      if (addPort)
        return hostName + (object) ":" + (string) (object) port;
      return hostName;
    }

    private bool TryGetHostUri(string hostName, out Uri hostUri)
    {
      StringBuilder stringBuilder = new StringBuilder(this._Uri.Scheme);
      string str1 = "://";
      stringBuilder.Append(str1);
      string str2 = hostName;
      stringBuilder.Append(str2);
      string pathAndQuery = this._Uri.PathAndQuery;
      stringBuilder.Append(pathAndQuery);
      return Uri.TryCreate(stringBuilder.ToString(), UriKind.Absolute, out hostUri);
    }

    private static string TrimScopeID(string s)
    {
      int length = s.LastIndexOf('%');
      if (length > 0)
        return s.Substring(0, length);
      return s;
    }

    private int GenerateProxyRequestLine(int headersSize)
    {
      if (this._Uri.Scheme == Uri.UriSchemeFtp)
        return this.GenerateFtpProxyRequestLine(headersSize);
      string components1 = this._Uri.GetComponents(UriComponents.Scheme | UriComponents.KeepDelimiter, UriFormat.UriEscaped);
      HostHeaderString hostHeaderString = new HostHeaderString(this.GetSafeHostAndPort(false, true));
      string components2 = this._Uri.GetComponents(UriComponents.PathAndQuery, UriFormat.UriEscaped);
      this.SetWriteBuffer(this.CurrentMethod.Name.Length + components1.Length + hostHeaderString.ByteCount + components2.Length + 12 + headersSize);
      int bytes = Encoding.ASCII.GetBytes(this.CurrentMethod.Name, 0, this.CurrentMethod.Name.Length, this.WriteBuffer, 0);
      byte[] writeBuffer1 = this.WriteBuffer;
      int index1 = bytes;
      int num1 = 1;
      int byteIndex1 = index1 + num1;
      int num2 = 32;
      writeBuffer1[index1] = (byte) num2;
      int destByteIndex = byteIndex1 + Encoding.ASCII.GetBytes(components1, 0, components1.Length, this.WriteBuffer, byteIndex1);
      hostHeaderString.Copy(this.WriteBuffer, destByteIndex);
      int byteIndex2 = destByteIndex + hostHeaderString.ByteCount;
      int num3 = byteIndex2 + Encoding.ASCII.GetBytes(components2, 0, components2.Length, this.WriteBuffer, byteIndex2);
      byte[] writeBuffer2 = this.WriteBuffer;
      int index2 = num3;
      int num4 = 1;
      int num5 = index2 + num4;
      int num6 = 32;
      writeBuffer2[index2] = (byte) num6;
      return num5;
    }

    private int GenerateFtpProxyRequestLine(int headersSize)
    {
      string components1 = this._Uri.GetComponents(UriComponents.Scheme | UriComponents.KeepDelimiter, UriFormat.UriEscaped);
      string s = this._Uri.GetComponents(UriComponents.UserInfo | UriComponents.KeepDelimiter, UriFormat.UriEscaped);
      HostHeaderString hostHeaderString = new HostHeaderString(this.GetSafeHostAndPort(false, true));
      string components2 = this._Uri.GetComponents(UriComponents.PathAndQuery, UriFormat.UriEscaped);
      if (s == "")
      {
        string str1 = (string) null;
        string str2 = (string) null;
        NetworkCredential credential = this.Credentials.GetCredential(this._Uri, "basic");
        if (credential != null && credential != FtpWebRequest.DefaultNetworkCredential)
        {
          str1 = credential.InternalGetDomainUserName();
          string password = credential.InternalGetPassword();
          str2 = password == null ? string.Empty : password;
        }
        if (str1 != null)
          s = str1.Replace(":", "%3A").Replace("\\", "%5C").Replace("/", "%2F").Replace("?", "%3F").Replace("#", "%23").Replace("%", "%25").Replace("@", "%40") + ":" + str2.Replace(":", "%3A").Replace("\\", "%5C").Replace("/", "%2F").Replace("?", "%3F").Replace("#", "%23").Replace("%", "%25").Replace("@", "%40") + "@";
      }
      this.SetWriteBuffer(this.CurrentMethod.Name.Length + components1.Length + s.Length + hostHeaderString.ByteCount + components2.Length + 12 + headersSize);
      int bytes = Encoding.ASCII.GetBytes(this.CurrentMethod.Name, 0, this.CurrentMethod.Name.Length, this.WriteBuffer, 0);
      byte[] writeBuffer1 = this.WriteBuffer;
      int index1 = bytes;
      int num1 = 1;
      int byteIndex1 = index1 + num1;
      int num2 = 32;
      writeBuffer1[index1] = (byte) num2;
      int byteIndex2 = byteIndex1 + Encoding.ASCII.GetBytes(components1, 0, components1.Length, this.WriteBuffer, byteIndex1);
      int destByteIndex = byteIndex2 + Encoding.ASCII.GetBytes(s, 0, s.Length, this.WriteBuffer, byteIndex2);
      hostHeaderString.Copy(this.WriteBuffer, destByteIndex);
      int byteIndex3 = destByteIndex + hostHeaderString.ByteCount;
      int num3 = byteIndex3 + Encoding.ASCII.GetBytes(components2, 0, components2.Length, this.WriteBuffer, byteIndex3);
      byte[] writeBuffer2 = this.WriteBuffer;
      int index2 = num3;
      int num4 = 1;
      int num5 = index2 + num4;
      int num6 = 32;
      writeBuffer2[index2] = (byte) num6;
      return num5;
    }

    private int GenerateRequestLine(int headersSize)
    {
      string pathAndQuery = this._Uri.PathAndQuery;
      this.SetWriteBuffer(this.CurrentMethod.Name.Length + pathAndQuery.Length + 12 + headersSize);
      int bytes = Encoding.ASCII.GetBytes(this.CurrentMethod.Name, 0, this.CurrentMethod.Name.Length, this.WriteBuffer, 0);
      byte[] writeBuffer1 = this.WriteBuffer;
      int index1 = bytes;
      int num1 = 1;
      int byteIndex = index1 + num1;
      int num2 = 32;
      writeBuffer1[index1] = (byte) num2;
      int num3 = byteIndex + Encoding.ASCII.GetBytes(pathAndQuery, 0, pathAndQuery.Length, this.WriteBuffer, byteIndex);
      byte[] writeBuffer2 = this.WriteBuffer;
      int index2 = num3;
      int num4 = 1;
      int num5 = index2 + num4;
      int num6 = 32;
      writeBuffer2[index2] = (byte) num6;
      return num5;
    }

    internal Uri GetRemoteResourceUri()
    {
      if (this.UseCustomHost)
        return this._HostUri;
      return this._Uri;
    }

    internal void UpdateHeaders()
    {
      bool addDefaultPort = this.IsTunnelRequest && this._OriginUri.Scheme == Uri.UriSchemeHttp;
      HostHeaderString hostHeaderString = new HostHeaderString(!this.UseCustomHost ? this.GetSafeHostAndPort(addDefaultPort, false) : HttpWebRequest.GetSafeHostAndPort(this._HostUri, this._HostHasPort | addDefaultPort, false));
      this._HttpRequestHeaders.ChangeInternal("Host", WebHeaderCollection.HeaderEncoding.GetString(hostHeaderString.Bytes, 0, hostHeaderString.ByteCount));
      if (this._CookieContainer == null)
        return;
      CookieModule.OnSendingHeaders(this);
    }

    internal void SerializeHeaders()
    {
      if (this.HttpWriteMode != HttpWriteMode.None)
      {
        if (this.HttpWriteMode == HttpWriteMode.Chunked)
          this._HttpRequestHeaders.AddInternal("Transfer-Encoding", "chunked");
        else if (this.ContentLength >= 0L)
          this._HttpRequestHeaders.ChangeInternal("Content-Length", this._ContentLength.ToString((IFormatProvider) NumberFormatInfo.InvariantInfo));
        this.ExpectContinue = this.ExpectContinue && !this.IsVersionHttp10 && this.ServicePoint.Expect100Continue;
        if ((this.ContentLength > 0L || this.HttpWriteMode == HttpWriteMode.Chunked) && this.ExpectContinue)
          this._HttpRequestHeaders.AddInternal("Expect", "100-continue");
      }
      string str = this._HttpRequestHeaders.Get("Accept-Encoding") ?? string.Empty;
      if ((this.AutomaticDecompression & DecompressionMethods.GZip) != DecompressionMethods.None && str.IndexOf("gzip", StringComparison.OrdinalIgnoreCase) < 0)
      {
        if ((this.AutomaticDecompression & DecompressionMethods.Deflate) != DecompressionMethods.None && str.IndexOf("deflate", StringComparison.OrdinalIgnoreCase) < 0)
          this._HttpRequestHeaders.AddInternal("Accept-Encoding", "gzip, deflate");
        else
          this._HttpRequestHeaders.AddInternal("Accept-Encoding", "gzip");
      }
      else if ((this.AutomaticDecompression & DecompressionMethods.Deflate) != DecompressionMethods.None && str.IndexOf("deflate", StringComparison.OrdinalIgnoreCase) < 0)
        this._HttpRequestHeaders.AddInternal("Accept-Encoding", "deflate");
      string name = "Connection";
      if (this.UsesProxySemantics || this.IsTunnelRequest)
      {
        this._HttpRequestHeaders.RemoveInternal("Connection");
        name = "Proxy-Connection";
        if (!ValidationHelper.IsBlankString(this.Connection))
          this._HttpRequestHeaders.AddInternal("Proxy-Connection", this._HttpRequestHeaders["Connection"]);
      }
      else
        this._HttpRequestHeaders.RemoveInternal("Proxy-Connection");
      if (this.IsWebSocketRequest && (this._HttpRequestHeaders.Get("Connection") ?? string.Empty).IndexOf("Upgrade", StringComparison.OrdinalIgnoreCase) < 0)
        this._HttpRequestHeaders.AddInternal("Connection", "Upgrade");
      if (this.KeepAlive || this.NtlmKeepAlive)
      {
        if (this.IsVersionHttp10 || this.ServicePoint.HttpBehaviour <= HttpBehaviour.HTTP10)
          this._HttpRequestHeaders.AddInternal(this.UsesProxySemantics || this.IsTunnelRequest ? "Proxy-Connection" : "Connection", "Keep-Alive");
      }
      else if (!this.IsVersionHttp10)
        this._HttpRequestHeaders.AddInternal(name, "Close");
      string myString = this._HttpRequestHeaders.ToString();
      int byteCount = WebHeaderCollection.HeaderEncoding.GetByteCount(myString);
      int dstOffset = !this.CurrentMethod.ConnectRequest ? (!this.UsesProxySemantics ? this.GenerateRequestLine(byteCount) : this.GenerateProxyRequestLine(byteCount)) : this.GenerateConnectRequestLine(byteCount);
      Buffer.BlockCopy((Array) HttpWebRequest.HttpBytes, 0, (Array) this.WriteBuffer, dstOffset, HttpWebRequest.HttpBytes.Length);
      int num1 = dstOffset + HttpWebRequest.HttpBytes.Length;
      byte[] writeBuffer1 = this.WriteBuffer;
      int index1 = num1;
      int num2 = 1;
      int num3 = index1 + num2;
      int num4 = 49;
      writeBuffer1[index1] = (byte) num4;
      byte[] writeBuffer2 = this.WriteBuffer;
      int index2 = num3;
      int num5 = 1;
      int num6 = index2 + num5;
      int num7 = 46;
      writeBuffer2[index2] = (byte) num7;
      byte[] writeBuffer3 = this.WriteBuffer;
      int index3 = num6;
      int num8 = 1;
      int num9 = index3 + num8;
      int num10 = this.IsVersionHttp10 ? 48 : 49;
      writeBuffer3[index3] = (byte) num10;
      byte[] writeBuffer4 = this.WriteBuffer;
      int index4 = num9;
      int num11 = 1;
      int num12 = index4 + num11;
      int num13 = 13;
      writeBuffer4[index4] = (byte) num13;
      byte[] writeBuffer5 = this.WriteBuffer;
      int index5 = num12;
      int num14 = 1;
      int num15 = index5 + num14;
      int num16 = 10;
      writeBuffer5[index5] = (byte) num16;
      if (Logging.On)
        Logging.PrintInfo(Logging.Web, (object) this, "Request: " + Encoding.ASCII.GetString(this.WriteBuffer, 0, num15));
      WebHeaderCollection.HeaderEncoding.GetBytes(myString, 0, myString.Length, this.WriteBuffer, num15);
    }

    [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter, SerializationFormatter = true)]
    void ISerializable.GetObjectData(SerializationInfo serializationInfo, StreamingContext streamingContext)
    {
      this.GetObjectData(serializationInfo, streamingContext);
    }

    /// <summary>
    /// Populates a <see cref="T:System.Runtime.Serialization.SerializationInfo"/> with the data required to serialize the target object.
    /// </summary>
    /// <param name="serializationInfo">The <see cref="T:System.Runtime.Serialization.SerializationInfo"/> to populate with data. </param><param name="streamingContext">A <see cref="T:System.Runtime.Serialization.StreamingContext"/> that specifies the destination for this serialization.</param>
    [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
    protected override void GetObjectData(SerializationInfo serializationInfo, StreamingContext streamingContext)
    {
      serializationInfo.AddValue("_HttpRequestHeaders", (object) this._HttpRequestHeaders, typeof (WebHeaderCollection));
      serializationInfo.AddValue("_Proxy", (object) this._Proxy, typeof (IWebProxy));
      serializationInfo.AddValue("_KeepAlive", this.KeepAlive);
      serializationInfo.AddValue("_Pipelined", this.Pipelined);
      serializationInfo.AddValue("_AllowAutoRedirect", this.AllowAutoRedirect);
      serializationInfo.AddValue("_AllowWriteStreamBuffering", this.AllowWriteStreamBuffering);
      serializationInfo.AddValue("_HttpWriteMode", (object) this.HttpWriteMode);
      serializationInfo.AddValue("_MaximumAllowedRedirections", this._MaximumAllowedRedirections);
      serializationInfo.AddValue("_AutoRedirects", this._AutoRedirects);
      serializationInfo.AddValue("_Timeout", this._Timeout);
      serializationInfo.AddValue("_ReadWriteTimeout", this._ReadWriteTimeout);
      serializationInfo.AddValue("_MaximumResponseHeadersLength", this._MaximumResponseHeadersLength);
      serializationInfo.AddValue("_ContentLength", this.ContentLength);
      serializationInfo.AddValue("_MediaType", (object) this._MediaType);
      serializationInfo.AddValue("_OriginVerb", (object) this._OriginVerb);
      serializationInfo.AddValue("_ConnectionGroupName", (object) this._ConnectionGroupName);
      serializationInfo.AddValue("_Version", (object) this.ProtocolVersion, typeof (Version));
      serializationInfo.AddValue("_OriginUri", (object) this._OriginUri, typeof (Uri));
      base.GetObjectData(serializationInfo, streamingContext);
    }

    internal static StringBuilder GenerateConnectionGroup(string connectionGroupName, bool unsafeConnectionGroup, bool isInternalGroup)
    {
      StringBuilder stringBuilder = new StringBuilder(connectionGroupName);
      stringBuilder.Append(unsafeConnectionGroup ? "U>" : "S>");
      if (isInternalGroup)
        stringBuilder.Append("I>");
      return stringBuilder;
    }

    internal string GetConnectionGroupLine()
    {
      StringBuilder stringBuilder = HttpWebRequest.GenerateConnectionGroup(this._ConnectionGroupName, this.UnsafeAuthenticatedConnectionSharing, this.m_InternalConnectionGroup);
      if (this._Uri.Scheme == Uri.UriSchemeHttps || this.IsTunnelRequest)
      {
        if (this.UsesProxy)
        {
          stringBuilder.Append(this.GetSafeHostAndPort(true, false));
          stringBuilder.Append("$");
        }
        if (this._ClientCertificates != null && this.ClientCertificates.Count > 0)
          stringBuilder.Append(this.ClientCertificates.GetHashCode().ToString((IFormatProvider) NumberFormatInfo.InvariantInfo));
        if (this.ServerCertificateValidationCallback != null)
        {
          stringBuilder.Append("&");
          stringBuilder.Append(HttpWebRequest.GetDelegateId(this.ServerCertificateValidationCallback));
        }
      }
      if (this.ProxyAuthenticationState.UniqueGroupId != null)
        stringBuilder.Append(this.ProxyAuthenticationState.UniqueGroupId);
      else if (this.ServerAuthenticationState.UniqueGroupId != null)
        stringBuilder.Append(this.ServerAuthenticationState.UniqueGroupId);
      return stringBuilder.ToString();
    }

    private static string GetDelegateId(RemoteCertificateValidationCallback callback)
    {
      try
      {
        new ReflectionPermission(PermissionState.Unrestricted).Assert();
        MethodInfo method = callback.Method;
        string name = callback.Method.Name;
        object target = callback.Target;
        return (target != null ? target.GetType().Name + "#" + target.GetHashCode().ToString((IFormatProvider) NumberFormatInfo.InvariantInfo) : method.DeclaringType.FullName) + "::" + name;
      }
      finally
      {
        CodeAccessPermission.RevertAssert();
      }
    }

    private bool CheckResubmitForAuth()
    {
      bool flag1 = false;
      bool flag2 = false;
      bool flag3 = false;
      if (this.UsesProxySemantics && this._Proxy != null)
      {
        if (this._Proxy.Credentials != null)
        {
          try
          {
            flag1 |= this.ProxyAuthenticationState.AttemptAuthenticate(this, this._Proxy.Credentials);
          }
          catch (Win32Exception ex)
          {
            if (!this.m_Extra401Retry)
              throw;
            else
              flag3 = true;
          }
          flag2 = true;
        }
      }
      if (this.Credentials != null)
      {
        if (!flag3)
        {
          try
          {
            flag1 |= this.ServerAuthenticationState.AttemptAuthenticate(this, this.Credentials);
          }
          catch (Win32Exception ex)
          {
            if (!this.m_Extra401Retry)
              throw;
            else
              flag1 = false;
          }
          flag2 = true;
        }
      }
      if (!flag1 & flag2 && this.m_Extra401Retry)
      {
        this.ClearAuthenticatedConnectionResources();
        this.m_Extra401Retry = false;
        flag1 = true;
      }
      return flag1;
    }

    private bool CheckResubmitForCache(ref Exception e)
    {
      if (!this.CheckCacheRetrieveOnResponse())
      {
        if (this.AllowAutoRedirect)
        {
          if (Logging.On)
            Logging.PrintWarning(Logging.Web, (object) this, "", SR.GetString("net_log_cache_validation_failed_resubmit"));
          return true;
        }
        if (Logging.On)
          Logging.PrintError(Logging.Web, (object) this, "", SR.GetString("net_log_cache_refused_server_response"));
        e = (Exception) new InvalidOperationException(SR.GetString("net_cache_not_accept_response"));
        return false;
      }
      this.CheckCacheUpdateOnResponse();
      return false;
    }

    private void SetExceptionIfRequired(string message, ref Exception e)
    {
      this.SetExceptionIfRequired(message, (Exception) null, ref e);
    }

    private void SetExceptionIfRequired(string message, Exception innerException, ref Exception e)
    {
      if (this._returnResponseOnFailureStatusCode)
      {
        if (!Logging.On)
          return;
        if (innerException != null)
          Logging.Exception(Logging.Web, (object) this, "", innerException);
        Logging.PrintWarning(Logging.Web, (object) this, "", message);
      }
      else
        e = (Exception) new WebException(message, innerException, WebExceptionStatus.ProtocolError, (WebResponse) this._HttpResponse);
    }

    private bool CheckResubmit(ref Exception e, ref bool disableUpload)
    {
      bool flag = false;
      if (this.ResponseStatusCode == HttpStatusCode.Unauthorized || this.ResponseStatusCode == HttpStatusCode.ProxyAuthenticationRequired)
      {
        try
        {
          if (!(flag = this.CheckResubmitForAuth()))
          {
            this.SetExceptionIfRequired(SR.GetString("net_servererror", new object[1]
            {
              (object) NetRes.GetWebStatusCodeString(this.ResponseStatusCode, this._HttpResponse.StatusDescription)
            }), ref e);
            return false;
          }
        }
        catch (Win32Exception ex)
        {
          throw new WebException(SR.GetString("net_servererror", new object[1]
          {
            (object) NetRes.GetWebStatusCodeString(this.ResponseStatusCode, this._HttpResponse.StatusDescription)
          }), (Exception) ex, WebExceptionStatus.ProtocolError, (WebResponse) this._HttpResponse);
        }
      }
      else
      {
        if (this.ServerAuthenticationState != null && this.ServerAuthenticationState.Authorization != null)
        {
          HttpWebResponse httpWebResponse = this._HttpResponse;
          if (httpWebResponse != null)
          {
            httpWebResponse.InternalSetIsMutuallyAuthenticated = this.ServerAuthenticationState.Authorization.MutuallyAuthenticated;
            if (this.AuthenticationLevel == AuthenticationLevel.MutualAuthRequired && !httpWebResponse.IsMutuallyAuthenticated)
              throw new WebException(SR.GetString("net_webstatus_RequestCanceled"), (Exception) new ProtocolViolationException(SR.GetString("net_mutualauthfailed")), WebExceptionStatus.RequestCanceled, (WebResponse) httpWebResponse);
          }
        }
        if (this.ResponseStatusCode == HttpStatusCode.BadRequest && this.SendChunked && (this.HttpWriteMode != HttpWriteMode.ContentLength && this.ServicePoint.InternalProxyServicePoint) && this.AllowWriteStreamBuffering)
        {
          this.ClearAuthenticatedConnectionResources();
          return true;
        }
        if (this.AllowAutoRedirect && (this.ResponseStatusCode == HttpStatusCode.MultipleChoices || this.ResponseStatusCode == HttpStatusCode.MovedPermanently || (this.ResponseStatusCode == HttpStatusCode.Found || this.ResponseStatusCode == HttpStatusCode.SeeOther) || this.ResponseStatusCode == HttpStatusCode.TemporaryRedirect))
        {
          this._AutoRedirects = this._AutoRedirects + 1;
          if (this._AutoRedirects > this._MaximumAllowedRedirections)
          {
            this.SetExceptionIfRequired(SR.GetString("net_tooManyRedirections"), ref e);
            return false;
          }
          string location = this._HttpResponse.Headers.Location;
          if (location == null)
          {
            this.SetExceptionIfRequired(SR.GetString("net_servererror", new object[1]
            {
              (object) NetRes.GetWebStatusCodeString(this.ResponseStatusCode, this._HttpResponse.StatusDescription)
            }), ref e);
            return false;
          }
          Uri uri1;
          try
          {
            uri1 = new Uri(this._Uri, location);
          }
          catch (UriFormatException ex)
          {
            this.SetExceptionIfRequired(SR.GetString("net_resubmitprotofailed"), (Exception) ex, ref e);
            return false;
          }
          if (this.IsWebSocketRequest)
          {
            if (uri1.Scheme == Uri.UriSchemeWs)
              uri1 = new UriBuilder(uri1)
              {
                Scheme = Uri.UriSchemeHttp
              }.Uri;
            else if (uri1.Scheme == Uri.UriSchemeWss)
              uri1 = new UriBuilder(uri1)
              {
                Scheme = Uri.UriSchemeHttps
              }.Uri;
          }
          if (uri1.Scheme != Uri.UriSchemeHttp && uri1.Scheme != Uri.UriSchemeHttps)
          {
            this.SetExceptionIfRequired(SR.GetString("net_resubmitprotofailed"), ref e);
            return false;
          }
          if (!this.HasRedirectPermission(uri1, ref e))
            return false;
          Uri uri2 = this._Uri;
          this._Uri = uri1;
          this._RedirectedToDifferentHost = (uint) Uri.Compare(this._OriginUri, this._Uri, UriComponents.HostAndPort, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) > 0U;
          if (this.UseCustomHost)
          {
            Uri hostUri;
            this.TryGetHostUri(HttpWebRequest.GetHostAndPortString(this._HostUri.Host, this._HostUri.Port, true), out hostUri);
            if (!this.HasRedirectPermission(hostUri, ref e))
            {
              this._Uri = uri2;
              return false;
            }
            this._HostUri = hostUri;
          }
          if (this.ResponseStatusCode > (HttpStatusCode) 299 && Logging.On)
            Logging.PrintWarning(Logging.Web, (object) this, "", SR.GetString("net_log_server_response_error_code", new object[1]
            {
              (object) ((int) this.ResponseStatusCode).ToString((IFormatProvider) NumberFormatInfo.InvariantInfo)
            }));
          if (this.HttpWriteMode != HttpWriteMode.None)
          {
            switch (this.ResponseStatusCode)
            {
              case HttpStatusCode.MovedPermanently:
              case HttpStatusCode.Found:
                if (this.CurrentMethod.Equals(KnownHttpVerb.Post))
                {
                  disableUpload = true;
                  goto case HttpStatusCode.TemporaryRedirect;
                }
                else
                  goto case HttpStatusCode.TemporaryRedirect;
              case HttpStatusCode.TemporaryRedirect:
                if (disableUpload)
                {
                  if (!this.AllowWriteStreamBuffering && this.IsOutstandingGetRequestStream)
                    return false;
                  this.CurrentMethod = KnownHttpVerb.Get;
                  this.ExpectContinue = false;
                  this.HttpWriteMode = HttpWriteMode.None;
                  break;
                }
                break;
              default:
                disableUpload = true;
                goto case HttpStatusCode.TemporaryRedirect;
            }
          }
          if (((ICredentials) (this.Credentials as CredentialCache) ?? (ICredentials) (this.Credentials as SystemNetworkCredential)) == null)
            this.Credentials = (ICredentials) null;
          this.ProxyAuthenticationState.ClearAuthReq(this);
          this.ServerAuthenticationState.ClearAuthReq(this);
          if (this._OriginUri.Scheme == Uri.UriSchemeHttps)
            this._HttpRequestHeaders.RemoveInternal("Referer");
        }
        else
        {
          if (this.ResponseStatusCode > (HttpStatusCode) 399)
          {
            this.SetExceptionIfRequired(SR.GetString("net_servererror", new object[1]
            {
              (object) NetRes.GetWebStatusCodeString(this.ResponseStatusCode, this._HttpResponse.StatusDescription)
            }), ref e);
            return false;
          }
          if (!this.AllowAutoRedirect || this.ResponseStatusCode <= (HttpStatusCode) 299)
            return false;
          this.SetExceptionIfRequired(SR.GetString("net_servererror", new object[1]
          {
            (object) NetRes.GetWebStatusCodeString(this.ResponseStatusCode, this._HttpResponse.StatusDescription)
          }), ref e);
          return false;
        }
      }
      if (this.HttpWriteMode != HttpWriteMode.None && !this.AllowWriteStreamBuffering && (this._resendRequestContent == null && this.UserRetrievedWriteStream) && (this.HttpWriteMode != HttpWriteMode.ContentLength || this.ContentLength != 0L))
      {
        e = (Exception) new WebException(SR.GetString("net_need_writebuffering"), (Exception) null, WebExceptionStatus.ProtocolError, (WebResponse) this._HttpResponse);
        return false;
      }
      if (!flag)
        this.ClearAuthenticatedConnectionResources();
      if (Logging.On)
        Logging.PrintWarning(Logging.Web, (object) this, "", SR.GetString("net_log_resubmitting_request"));
      return true;
    }

    private bool HasRedirectPermission(Uri uri, ref Exception resultException)
    {
      try
      {
        this.CheckConnectPermission(uri, this.Async);
      }
      catch (SecurityException ex)
      {
        resultException = (Exception) new SecurityException(SR.GetString("net_redirect_perm"), (Exception) new WebException(SR.GetString("net_resubmitcanceled"), (Exception) ex, WebExceptionStatus.ProtocolError, (WebResponse) this._HttpResponse));
        return false;
      }
      return true;
    }

    private void CheckConnectPermission(Uri uri, bool needExecutionContext)
    {
      ExecutionContext executionContext = needExecutionContext ? this.GetReadingContext().ContextCopy : (ExecutionContext) null;
      CodeAccessPermission accessPermission = (CodeAccessPermission) new WebPermission(NetworkAccess.Connect, uri);
      if (executionContext == null)
        accessPermission.Demand();
      else
        ExecutionContext.Run(executionContext, NclUtilities.ContextRelativeDemandCallback, (object) accessPermission);
    }

    private void ClearRequestForResubmit(bool ntlmFollowupRequest)
    {
      this._HttpRequestHeaders.RemoveInternal("Host");
      this._HttpRequestHeaders.RemoveInternal("Connection");
      this._HttpRequestHeaders.RemoveInternal("Proxy-Connection");
      this._HttpRequestHeaders.RemoveInternal("Content-Length");
      this._HttpRequestHeaders.RemoveInternal("Transfer-Encoding");
      this._HttpRequestHeaders.RemoveInternal("Expect");
      if (this._HttpResponse != null && this._HttpResponse.ResponseStream != null)
      {
        if (!this._HttpResponse.KeepAlive)
        {
          ConnectStream connectStream = this._HttpResponse.ResponseStream as ConnectStream;
          if (connectStream != null)
            connectStream.ErrorResponseNotify(false);
        }
        ICloseEx closeEx = this._HttpResponse.ResponseStream as ICloseEx;
        if (closeEx != null)
          closeEx.CloseEx(CloseExState.Silent);
        else
          this._HttpResponse.ResponseStream.Close();
      }
      this._AbortDelegate = (HttpAbortDelegate) null;
      this.m_BodyStarted = false;
      this.HeadersCompleted = false;
      this._WriteBufferLength = 0;
      this.m_Extra401Retry = false;
      HttpWebResponse httpWebResponse = this._HttpResponse;
      this._HttpResponse = (HttpWebResponse) null;
      this.m_ContinueGate.Reset();
      this._RerequestCount = this._RerequestCount + 1;
      if (!this.Aborted && this.Async)
        this._CoreResponse = (object) null;
      if (this._SubmitWriteStream == null)
        return;
      if ((httpWebResponse != null && httpWebResponse.KeepAlive || this._SubmitWriteStream.IgnoreSocketErrors) && this.HasEntityBody)
      {
        if (this.AllowWriteStreamBuffering)
          this.SetRequestContinue();
        if (ntlmFollowupRequest)
        {
          this.NeedsToReadForResponse = !this.ShouldWaitFor100Continue();
          this._SubmitWriteStream.CallDone();
        }
        else if (!this.AllowWriteStreamBuffering)
        {
          this.NeedsToReadForResponse = !this.ShouldWaitFor100Continue();
          this._SubmitWriteStream.CloseInternal(true);
        }
        else if (!this.Async && this.UserRetrievedWriteStream)
          this._SubmitWriteStream.CallDone();
      }
      if (!this.Async && !this.UserRetrievedWriteStream || (this._OldSubmitWriteStream == null || this._OldSubmitWriteStream == this._SubmitWriteStream))
        return;
      this._SubmitWriteStream.CloseInternal(true);
    }

    private void FinishRequest(HttpWebResponse response, Exception errorException)
    {
      if (!this._ReadAResult.InternalPeekCompleted && this.m_Aborted != 1 && (response != null && errorException != null))
        response.ResponseStream = this.MakeMemoryStream(response.ResponseStream);
      if (errorException != null && this._SubmitWriteStream != null && !this._SubmitWriteStream.IsClosed)
        this._SubmitWriteStream.ErrorResponseNotify(this._SubmitWriteStream.Connection.KeepAlive);
      if (errorException != null || this._HttpResponse == null || this._HttpWriteMode != HttpWriteMode.Chunked && this._ContentLength <= 0L || (!this.ExpectContinue || this.Saw100Continue || (!this._ServicePoint.Understands100Continue || this.IsTunnelRequest)) || this.ResponseStatusCode > (HttpStatusCode) 299)
        return;
      this._ServicePoint.Understands100Continue = false;
    }

    private Stream MakeMemoryStream(Stream stream)
    {
      if (stream == null || stream is SyncMemoryStream)
        return stream;
      SyncMemoryStream syncMemoryStream = new SyncMemoryStream(0);
      try
      {
        if (stream.CanRead)
        {
          byte[] buffer = new byte[1024];
          int val2 = HttpWebRequest.DefaultMaximumErrorResponseLength == -1 ? buffer.Length : HttpWebRequest.DefaultMaximumErrorResponseLength * 1024;
          int count;
          while ((count = stream.Read(buffer, 0, Math.Min(buffer.Length, val2))) > 0)
          {
            syncMemoryStream.Write(buffer, 0, count);
            if (HttpWebRequest.DefaultMaximumErrorResponseLength != -1)
              val2 -= count;
          }
        }
        syncMemoryStream.Position = 0L;
      }
      catch
      {
      }
      finally
      {
        try
        {
          ICloseEx closeEx = stream as ICloseEx;
          if (closeEx != null)
            closeEx.CloseEx(CloseExState.Silent);
          else
            stream.Close();
        }
        catch
        {
        }
      }
      return (Stream) syncMemoryStream;
    }

    /// <summary>
    /// Adds a byte range header to the request for a specified range.
    /// </summary>
    /// <param name="from">The position at which to start sending data. </param><param name="to">The position at which to stop sending data. </param><exception cref="T:System.ArgumentException"><paramref name="rangeSpecifier"/> is invalid. </exception><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="from"/> is greater than <paramref name="to"/>-or- <paramref name="from"/> or <paramref name="to"/> is less than 0. </exception><exception cref="T:System.InvalidOperationException">The range header could not be added. </exception>
    public void AddRange(int from, int to)
    {
      this.AddRange("bytes", (long) from, (long) to);
    }

    /// <summary>
    /// Adds a byte range header to the request for a specified range.
    /// </summary>
    /// <param name="from">The position at which to start sending data.</param><param name="to">The position at which to stop sending data.</param><exception cref="T:System.ArgumentException"><paramref name="rangeSpecifier"/> is invalid. </exception><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="from"/> is greater than <paramref name="to"/>-or- <paramref name="from"/> or <paramref name="to"/> is less than 0. </exception><exception cref="T:System.InvalidOperationException">The range header could not be added. </exception>
    public void AddRange(long from, long to)
    {
      this.AddRange("bytes", from, to);
    }

    /// <summary>
    /// Adds a byte range header to a request for a specific range from the beginning or end of the requested data.
    /// </summary>
    /// <param name="range">The starting or ending point of the range. </param><exception cref="T:System.ArgumentException"><paramref name="rangeSpecifier"/> is invalid. </exception><exception cref="T:System.InvalidOperationException">The range header could not be added. </exception>
    public void AddRange(int range)
    {
      this.AddRange("bytes", (long) range);
    }

    /// <summary>
    /// Adds a byte range header to a request for a specific range from the beginning or end of the requested data.
    /// </summary>
    /// <param name="range">The starting or ending point of the range.</param><exception cref="T:System.ArgumentException"><paramref name="rangeSpecifier"/> is invalid. </exception><exception cref="T:System.InvalidOperationException">The range header could not be added. </exception>
    public void AddRange(long range)
    {
      this.AddRange("bytes", range);
    }

    /// <summary>
    /// Adds a range header to a request for a specified range.
    /// </summary>
    /// <param name="rangeSpecifier">The description of the range. </param><param name="from">The position at which to start sending data. </param><param name="to">The position at which to stop sending data. </param><exception cref="T:System.ArgumentNullException"><paramref name="rangeSpecifier"/> is null. </exception><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="from"/> is greater than <paramref name="to"/>-or- <paramref name="from"/> or <paramref name="to"/> is less than 0. </exception><exception cref="T:System.ArgumentException"><paramref name="rangeSpecifier"/> is invalid. </exception><exception cref="T:System.InvalidOperationException">The range header could not be added. </exception>
    public void AddRange(string rangeSpecifier, int from, int to)
    {
      this.AddRange(rangeSpecifier, (long) from, (long) to);
    }

    /// <summary>
    /// Adds a range header to a request for a specified range.
    /// </summary>
    /// <param name="rangeSpecifier">The description of the range.</param><param name="from">The position at which to start sending data.</param><param name="to">The position at which to stop sending data.</param><exception cref="T:System.ArgumentNullException"><paramref name="rangeSpecifier"/> is null. </exception><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="from"/> is greater than <paramref name="to"/>-or- <paramref name="from"/> or <paramref name="to"/> is less than 0. </exception><exception cref="T:System.ArgumentException"><paramref name="rangeSpecifier"/> is invalid. </exception><exception cref="T:System.InvalidOperationException">The range header could not be added. </exception>
    public void AddRange(string rangeSpecifier, long from, long to)
    {
      if (rangeSpecifier == null)
        throw new ArgumentNullException("rangeSpecifier");
      if (from < 0L || to < 0L)
        throw new ArgumentOutOfRangeException("from, to", SR.GetString("net_rangetoosmall"));
      if (from > to)
        throw new ArgumentOutOfRangeException("from", SR.GetString("net_fromto"));
      if (!WebHeaderCollection.IsValidToken(rangeSpecifier))
        throw new ArgumentException(SR.GetString("net_nottoken"), "rangeSpecifier");
      if (!this.AddRange(rangeSpecifier, from.ToString((IFormatProvider) NumberFormatInfo.InvariantInfo), to.ToString((IFormatProvider) NumberFormatInfo.InvariantInfo)))
        throw new InvalidOperationException(SR.GetString("net_rangetype"));
    }

    /// <summary>
    /// Adds a Range header to a request for a specific range from the beginning or end of the requested data.
    /// </summary>
    /// <param name="rangeSpecifier">The description of the range. </param><param name="range">The starting or ending point of the range. </param><exception cref="T:System.ArgumentNullException"><paramref name="rangeSpecifier"/> is null. </exception><exception cref="T:System.ArgumentException"><paramref name="rangeSpecifier"/> is invalid. </exception><exception cref="T:System.InvalidOperationException">The range header could not be added. </exception>
    public void AddRange(string rangeSpecifier, int range)
    {
      this.AddRange(rangeSpecifier, (long) range);
    }

    /// <summary>
    /// Adds a Range header to a request for a specific range from the beginning or end of the requested data.
    /// </summary>
    /// <param name="rangeSpecifier">The description of the range.</param><param name="range">The starting or ending point of the range.</param><exception cref="T:System.ArgumentNullException"><paramref name="rangeSpecifier"/> is null. </exception><exception cref="T:System.ArgumentException"><paramref name="rangeSpecifier"/> is invalid. </exception><exception cref="T:System.InvalidOperationException">The range header could not be added. </exception>
    public void AddRange(string rangeSpecifier, long range)
    {
      if (rangeSpecifier == null)
        throw new ArgumentNullException("rangeSpecifier");
      if (!WebHeaderCollection.IsValidToken(rangeSpecifier))
        throw new ArgumentException(SR.GetString("net_nottoken"), "rangeSpecifier");
      if (!this.AddRange(rangeSpecifier, range.ToString((IFormatProvider) NumberFormatInfo.InvariantInfo), range >= 0L ? "" : (string) null))
        throw new InvalidOperationException(SR.GetString("net_rangetype"));
    }

    private bool AddRange(string rangeSpecifier, string from, string to)
    {
      string str1 = this._HttpRequestHeaders["Range"];
      string str2;
      if (str1 == null || str1.Length == 0)
      {
        str2 = rangeSpecifier + "=";
      }
      else
      {
        if (string.Compare(str1.Substring(0, str1.IndexOf('=')), rangeSpecifier, StringComparison.OrdinalIgnoreCase) != 0)
          return false;
        str2 = string.Empty;
      }
      string str3 = str2 + from.ToString();
      if (to != null)
        str3 = str3 + "-" + to;
      this._HttpRequestHeaders.SetAddVerified("Range", str3);
      return true;
    }

    private static int GetStatusCode(HttpWebResponse httpWebResponse)
    {
      int num = -1;
      if (FrameworkEventSource.Log.IsEnabled())
      {
        if (httpWebResponse != null)
        {
          try
          {
            num = (int) httpWebResponse.StatusCode;
          }
          catch (ObjectDisposedException ex)
          {
          }
        }
      }
      return num;
    }

    private static class AbortState
    {
      public const int Public = 1;
      public const int Internal = 2;
    }

    [System.Flags]
    private enum Booleans : uint
    {
      AllowAutoRedirect = 1,
      AllowWriteStreamBuffering = 2,
      ExpectContinue = 4,
      ProxySet = 16,
      UnsafeAuthenticatedConnectionSharing = 64,
      IsVersionHttp10 = 128,
      SendChunked = 256,
      EnableDecompression = 512,
      IsTunnelRequest = 1024,
      IsWebSocketRequest = 2048,
      Default = ExpectContinue | AllowWriteStreamBuffering | AllowAutoRedirect,
    }
  }
}

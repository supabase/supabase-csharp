using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Serialization;
using Supabase.Core.Attributes;

namespace Supabase.Functions;

public partial class Client
{
    /// <summary>
    /// Options that can be supplied to a function invocation.
    ///
    /// Note: If Headers.Authorization is set, it can be later overriden if a token is supplied in the method call or set with SetAuth.
    /// </summary>
    public class InvokeFunctionOptions
    {
        /// <summary>
        /// Headers to be included on the request.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } =
            new Dictionary<string, string>();

        /// <summary>
        /// Body of the Request
        /// </summary>
        [JsonPropertyName("body")]
        public Dictionary<string, object> Body { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Timout value for HttpClient Requests, defaults to 100s.
        /// https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.timeout?view=net-8.0#remarks
        /// </summary>
        [PartialCapability("functions.invocation.timeout", "Per-invoke HttpTimeout exists, but it is not composable with a caller-supplied cancellation signal (Invoke accepts no CancellationToken).")]
        public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(100);

        /// <summary>
        /// Http method of the Request
        /// </summary>
        [ImplementedCapability("functions.invocation.method_override", "InvokeFunctionOptions.HttpMethod is wired into the request (default POST). [feature-parity.md called this POST-only; the source disagrees as of 2026-06-29.]")]
        public HttpMethod HttpMethod { get; set; } = HttpMethod.Post;

        /// <summary>
        /// Region of the request
        /// </summary>
        [ImplementedCapability("functions.invocation.region_selection", "Full AWS region list; per-call override or client default; emits x-region.")]
        public FunctionRegion? FunctionRegion { get; set; } = null;
    }

    /// <summary>
    /// Define the region for requests
    /// </summary>
    [ImplementedCapability("functions.invocation.region_selection", "Full AWS region list; per-call override or client default; emits x-region.")]
    public class FunctionRegion : IEquatable<FunctionRegion>
    {
        private readonly string region;

        /// <summary>
        /// Empty region
        /// </summary>
        public static FunctionRegion Any { get; } = new FunctionRegion("any");

        /// <summary>
        /// Represents the region "ap-northeast-1" for function requests.
        /// </summary>
        public static FunctionRegion ApNortheast1 { get; } =
            new FunctionRegion("ap-northeast-1");

        /// <summary>
        /// Represents the "ap-northeast-2" region for function invocation.
        /// </summary>
        public static FunctionRegion ApNortheast2 { get; } =
            new FunctionRegion("ap-northeast-2");

        /// <summary>
        /// Represents the "ap-south-1" region used for requests.
        /// </summary>
        public static FunctionRegion ApSouth1 { get; } = new FunctionRegion("ap-south-1");

        /// <summary>
        /// Represents the region "ap-southeast-1" for function invocation.
        /// </summary>
        public static FunctionRegion ApSoutheast1 { get; } =
            new FunctionRegion("ap-southeast-1");

        /// <summary>
        /// Represents the "ap-southeast-2" region for requests.
        /// </summary>
        public static FunctionRegion ApSoutheast2 { get; } =
            new FunctionRegion("ap-southeast-2");

        /// <summary>
        /// Represents the Canada (Central) region for requests.
        /// </summary>
        public static FunctionRegion CaCentral1 { get; } = new FunctionRegion("ca-central-1");

        /// <summary>
        /// Represents the "eu-central-1" region for function invocation.
        /// </summary>
        public static FunctionRegion EuCentral1 { get; } = new FunctionRegion("eu-central-1");

        /// <summary>
        /// Represents the "eu-west-1" function region for requests.
        /// </summary>
        public static FunctionRegion EuWest1 { get; } = new FunctionRegion("eu-west-1");

        /// <summary>
        /// Represents the "eu-west-2" region for function invocation requests.
        /// </summary>
        public static FunctionRegion EuWest2 { get; } = new FunctionRegion("eu-west-2");

        /// <summary>
        /// Represents the AWS region 'eu-west-3'.
        /// </summary>
        public static FunctionRegion EuWest3 { get; } = new FunctionRegion("eu-west-3");

        /// <summary>
        /// Represents the South America (São Paulo) region for requests.
        /// </summary>
        public static FunctionRegion SaEast1 { get; } = new FunctionRegion("sa-east-1");

        /// <summary>
        /// Represents the "us-east-1" region for function requests.
        /// </summary>
        public static FunctionRegion UsEast1 { get; } = new FunctionRegion("us-east-1");

        /// <summary>
        /// Represents the us-west-1 region for function requests.
        /// </summary>
        public static FunctionRegion UsWest1 { get; } = new FunctionRegion("us-west-1");

        /// <summary>
        /// Represents the "us-west-2" region for requests.
        /// </summary>
        public static FunctionRegion UsWest2 { get; } = new FunctionRegion("us-west-2");

        /// <summary>
        /// Define the region for requests
        /// </summary>
        public FunctionRegion(string region) => this.region = region;

        /// <summary>
        /// Check if the object is identical to the reference passed
        /// </summary>
        public override bool Equals(object obj) => obj is FunctionRegion r && this.Equals(r);

        /// <summary>
        /// Generate Hash code
        /// </summary>
        public override int GetHashCode() => this.region.GetHashCode();

        /// <summary>
        /// Check if the object is identical to the reference passed
        /// </summary>
        public bool Equals(FunctionRegion other) => this.region == other.region;

        /// <summary>
        /// Overloading the operator ==
        /// </summary>
        public static bool operator ==(FunctionRegion? left, FunctionRegion? right) =>
            Equals(left, right);

        /// <summary>
        /// Overloading the operator !=
        /// </summary>
        public static bool operator !=(FunctionRegion? left, FunctionRegion? right) =>
            !Equals(left, right);

        /// <summary>
        /// Overloads the explicit cast operator to convert a FunctionRegion object to a string.
        /// </summary>
        public static explicit operator string(FunctionRegion region) => region.ToString();

        /// <summary>
        /// Overloads the explicit cast operator to convert a string to a FunctionRegion object.
        /// </summary>
        public static explicit operator FunctionRegion(string region) =>
            new FunctionRegion(region);

        /// <summary>
        /// Returns a string representation of the FunctionRegion instance.
        /// </summary>
        /// <returns>A string that represents the current FunctionRegion instance.</returns>
        public override string ToString() => this.region;
    }
}

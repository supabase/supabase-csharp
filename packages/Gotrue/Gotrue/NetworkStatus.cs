using System.Collections.Generic;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using Supabase.Core.Http;
using Supabase.Gotrue.Interfaces;
namespace Supabase.Gotrue;

/// <summary>
/// A Network status system to pair with the <see cref="Client.Online"/>Client.
///
/// <see>
///     <cref>https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/network-info</cref>
/// </see>
/// </summary>
public class NetworkStatus
{
    private readonly List<NetworkListener> listeners = new List<NetworkListener>();
    private readonly HttpClient httpClient;

    /// <summary>
    /// Creates a network status system, optionally sending ping checks through the given client.
    /// </summary>
    /// <param name="httpClient">The client to send ping checks through. Defaults to a self-owned client when null.</param>
    public NetworkStatus(HttpClient? httpClient = null) => this.httpClient = httpClient ?? DefaultHttpClientFactory.Create();

    /// <summary>
    /// True if the network has been checked.
    /// </summary>
    public bool Ready;

    /// <summary>
    /// A delegate for listening to network changes.
    /// </summary>
    public delegate void NetworkListener(bool isNetworkAvailable);

    /// <summary>
    /// Adds a listener to the network status system.
    /// </summary>
    /// <param name="listener"></param>
    public void AddListener(NetworkListener listener) => this.listeners.Add(listener);

    /// <summary>
    /// Removes a listener from the network status system.
    /// </summary>
    /// <param name="listener"></param>
    public void RemoveListener(NetworkListener listener) => this.listeners.Remove(listener);

    private void NotifyListeners(bool isNetworkAvailable)
    {
        foreach (var listener in this.listeners)
        {
            listener?.Invoke(isNetworkAvailable);
        }
    }

    /// <summary>
    /// The <see cref="Client"/> that this network status system is attached to.
    /// </summary>
    public IGotrueClient<User, Session>? Client { get; set; }

    /// <summary>
    /// Pings the URL in the <see cref="Client.Options"/> to check if the network is online.
    /// 
    /// https://PROJECTID.supabase.co/auth/v1/settings
    /// </summary>
    public async Task<bool> PingCheckAsync(string url)
    {
        try
        {
            var reply = await this.httpClient.GetAsync(url);
            if (reply?.StatusCode == System.Net.HttpStatusCode.OK)
            {
                this.UpdateStatus(true);
                return true;
            }
            this.UpdateStatus(false);
        }
        catch (HttpRequestException e)
        {
            this.Client?.Debug($"Network Problem: {e.Message}");
            this.UpdateStatus(false);
        }
        catch (SocketException e)
        {
            this.Client?.Debug($"Network Problem: {e.Message}");
            this.UpdateStatus(false);
        }
        catch (PingException e)
        {
            this.Client?.Debug($"Network Problem: {e.Message}");
            this.UpdateStatus(false);
        }
        return false;
    }

    private void UpdateStatus(bool isNetworkAvailable)
    {
        this.Ready = true;
        this.NotifyListeners(isNetworkAvailable);
        if (this.Client != null)
            this.Client.Online = isNetworkAvailable;
    }

    /// <summary>
    /// Starts the network status system. This will listen to the OS for network changes,
    /// and also does a ping check to confirm the current network status.
    /// </summary>
    public async Task<bool> StartAsync(string url)
    {
        NetworkChange.NetworkAvailabilityChanged += this.OnNetworkAvailabilityChanged;
        return await this.PingCheckAsync(url);
    }

    private void OnNetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e) => this.UpdateStatus(e.IsAvailable);

    /// <summary>
    /// Removes the network status system checker from the OS.
    /// </summary>
    ~NetworkStatus()
    {
        NetworkChange.NetworkAvailabilityChanged -= this.OnNetworkAvailabilityChanged;
    }
}

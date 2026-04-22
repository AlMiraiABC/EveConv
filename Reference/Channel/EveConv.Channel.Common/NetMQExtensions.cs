using NetMQ;

namespace EveConv.Channel.Common;

/// <summary>
/// Extensions for <see cref="NetMQ"/>
/// </summary>
public static class NetMQExtensions
{
    extension(NetMQPoller poller)
    {
        /// <summary>
        /// Wait until the <see cref="poller"/> is started/running.
        /// </summary>
        /// <remarks>Equals to <c>WaitForStart(Timeout.Infinite)</c></remarks>
        public void WaitForStart()
        {
            if (poller.IsRunning)
            {
                return;
            }
            SpinWait.SpinUntil(() => poller.IsRunning);
        }

        /// <summary>
        /// Wait until the <see cref="poller"/> is started/running or until the specified <see cref="Timeout"/> is expired.
        /// </summary>
        /// <param name="timeout">Time to wait. <b>DO NOT</b> set to <see cref="Timeout.Infinite"/>.</param>
        /// <exception cref="TimeoutException">The specified <paramref name="timeout"/> is expired.</exception>
        public void WaitForStart(TimeSpan timeout)
        {
            if (poller.IsRunning)
            {
                return;
            }
            if (SpinWait.SpinUntil(() => poller.IsRunning, timeout))
            {
                return;
            }
            throw new TimeoutException($"Poller failed to start after {timeout.TotalSeconds} seconds.");
        }
    }
}

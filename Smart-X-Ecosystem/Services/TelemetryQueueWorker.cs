using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Smart_X_Ecosystem.Services
{
    public class TelemetryQueueWorker : BackgroundService
    {
        private readonly CommandStreamService _streamService;

        public TelemetryQueueWorker(CommandStreamService streamService)
        {
            _streamService = streamService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1500));

            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                // Automatically drains CriticalAlertQueue (Priority 1) first, then TelemetryQueue (FIFO)
                while (_streamService.CriticalAlertQueue.Count > 0)
                {
                    _streamService.DequeueAndProcessNextPacketInBackground();
                }

                if (_streamService.TelemetryQueue.Count > 0)
                {
                    _streamService.DequeueAndProcessNextPacketInBackground();
                }
            }
        }
    }
}

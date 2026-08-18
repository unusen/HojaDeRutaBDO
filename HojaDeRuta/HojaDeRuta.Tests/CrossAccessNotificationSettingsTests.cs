using HojaDeRuta.Models.Config;
using HojaDeRuta.Models.DAO;
using HojaDeRuta.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HojaDeRuta.Tests;

public class CrossAccessNotificationSettingsTests
{
    [Fact]
    public async Task QueueCrossAccessAsync_DoesNotQueue_WhenDisabledByConfiguration()
    {
        var service = new NotificationQueueService(
            new NullDistributedCache(),
            null!,
            Options.Create(new MailSettings { HabilitarNotificacionCruzadas = false }),
            NullLogger<NotificationQueueService>.Instance);

        await service.QueueCrossAccessAsync(new Hoja { Id = "BANK3943" }, "https://example.test/hoja");

        var statuses = await service.GetStatusesAsync("BANK3943");
        Assert.Empty(statuses);
    }

    [Fact]
    public async Task SendCrossAccessAsync_DoesNotSend_WhenDisabledByConfiguration()
    {
        var service = new NotificationDeliveryService(
            Options.Create(new MailSettings { HabilitarNotificacionCruzadas = false }),
            null!,
            null!,
            NullLogger<NotificationDeliveryService>.Instance);

        await service.SendCrossAccessAsync(new Hoja { Id = "BANK3943", Sector = "BANK" }, "https://example.test/hoja");
    }

    private sealed class NullDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => null;

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]?>(null);

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Remove(string key)
        {
        }

        public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => Task.CompletedTask;
    }
}

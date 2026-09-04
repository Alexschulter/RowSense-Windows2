using RowSenseWindows.Models;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace RowSenseWindows.Services;

public sealed record BleDeviceInfo(ulong Address, string Name)
{
    public string Display => $"{Name}  [{Address:X12}]";
}

public sealed class BluetoothService : IDisposable
{
    public static readonly Guid ServiceUuid = Guid.Parse("8A4F1000-7C2B-4E91-A6D5-52A19F3C7001");
    public static readonly Guid ControlRxUuid = Guid.Parse("8A4F1001-7C2B-4E91-A6D5-52A19F3C7001");
    public static readonly Guid ControlTxUuid = Guid.Parse("8A4F1002-7C2B-4E91-A6D5-52A19F3C7001");
    public static readonly Guid LiveTxUuid = Guid.Parse("8A4F1003-7C2B-4E91-A6D5-52A19F3C7001");
    public static readonly Guid StatusTxUuid = Guid.Parse("8A4F1005-7C2B-4E91-A6D5-52A19F3C7001");

    private BluetoothLEAdvertisementWatcher? _watcher;
    private BluetoothLEDevice? _device;
    private GattCharacteristic? _controlRx;
    private GattCharacteristic? _controlTx;
    private GattCharacteristic? _liveTx;
    private GattCharacteristic? _statusTx;
    private readonly HashSet<ulong> _seen = new();

    public bool IsConnected => _device is not null && _liveTx is not null && _controlRx is not null;

    public event Action<BleDeviceInfo>? DeviceFound;
    public event Action<StrokeData>? StrokeReceived;
    public event Action<string>? StatusChanged;

    public void StartScan()
    {
        StopScan();
        _seen.Clear();

        _watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active
        };
        _watcher.Received += OnAdvertisementReceived;
        _watcher.Stopped += (_, _) => StatusChanged?.Invoke("Сканирование остановлено");
        _watcher.Start();
        StatusChanged?.Invoke("Поиск RowSense-Central...");
    }

    public void StopScan()
    {
        if (_watcher is null) return;
        try { _watcher.Stop(); } catch { }
        _watcher.Received -= OnAdvertisementReceived;
        _watcher = null;
    }

    private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
    {
        var adv = args.Advertisement;
        bool serviceMatch = adv.ServiceUuids.Contains(ServiceUuid);
        string name = adv.LocalName ?? string.Empty;
        bool nameMatch = string.Equals(name, "RowSense-Central", StringComparison.OrdinalIgnoreCase);
        if (!serviceMatch && !nameMatch) return;

        lock (_seen)
        {
            if (!_seen.Add(args.BluetoothAddress)) return;
        }
        DeviceFound?.Invoke(new BleDeviceInfo(args.BluetoothAddress, string.IsNullOrWhiteSpace(name) ? "RowSense-Central" : name));
    }

    public async Task<bool> ConnectAsync(BleDeviceInfo info)
    {
        await DisconnectAsync();
        StopScan();
        StatusChanged?.Invoke("Подключение...");

        _device = await BluetoothLEDevice.FromBluetoothAddressAsync(info.Address);
        if (_device is null)
        {
            StatusChanged?.Invoke("Не удалось открыть BLE-устройство");
            return false;
        }

        _device.ConnectionStatusChanged += OnConnectionStatusChanged;

        var servicesResult = await _device.GetGattServicesForUuidAsync(ServiceUuid, BluetoothCacheMode.Uncached);
        if (servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
        {
            StatusChanged?.Invoke("Сервис RowSense BLE не найден");
            await DisconnectAsync();
            return false;
        }

        var service = servicesResult.Services[0];
        _controlRx = await FindCharacteristic(service, ControlRxUuid);
        _controlTx = await FindCharacteristic(service, ControlTxUuid);
        _liveTx = await FindCharacteristic(service, LiveTxUuid);
        _statusTx = await FindCharacteristic(service, StatusTxUuid);

        if (_controlRx is null || _liveTx is null)
        {
            StatusChanged?.Invoke("Не найдены CONTROL_RX или LIVE_TX");
            await DisconnectAsync();
            return false;
        }

        _liveTx.ValueChanged += LiveTxOnValueChanged;
        var notifyStatus = await _liveTx.WriteClientCharacteristicConfigurationDescriptorAsync(
            GattClientCharacteristicConfigurationDescriptorValue.Notify);
        if (notifyStatus != GattCommunicationStatus.Success)
        {
            StatusChanged?.Invoke("Не удалось включить LIVE_TX notifications");
            await DisconnectAsync();
            return false;
        }

        if (_controlTx is not null)
        {
            _controlTx.ValueChanged += ControlTxOnValueChanged;
            await _controlTx.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify);
        }

        if (_statusTx is not null)
        {
            _statusTx.ValueChanged += StatusTxOnValueChanged;
            await _statusTx.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify);
        }

        StatusChanged?.Invoke($"Подключено: {_device.Name}");
        return true;
    }

    private static async Task<GattCharacteristic?> FindCharacteristic(GattDeviceService service, Guid uuid)
    {
        var result = await service.GetCharacteristicsForUuidAsync(uuid, BluetoothCacheMode.Uncached);
        return result.Status == GattCommunicationStatus.Success && result.Characteristics.Count > 0
            ? result.Characteristics[0]
            : null;
    }

    private void LiveTxOnValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        var bytes = ReadBytes(args.CharacteristicValue);
        if (StrokeData.TryParse(bytes, out var stroke) && stroke is not null)
            StrokeReceived?.Invoke(stroke);
    }

    private void ControlTxOnValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        var b = ReadBytes(args.CharacteristicValue);
        if (b.Length >= 2)
            StatusChanged?.Invoke($"CONTROL 0x{b[0]:X2}, result 0x{b[1]:X2}");
    }

    private void StatusTxOnValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        var b = ReadBytes(args.CharacteristicValue);
        if (b.Length >= 4)
            StatusChanged?.Invoke($"ESP status: protocol {b[0]}.{b[1]}, recording={b[2]}, SD={b[3]}");
    }

    private static byte[] ReadBytes(IBuffer buffer)
    {
        using var reader = DataReader.FromBuffer(buffer);
        var bytes = new byte[reader.UnconsumedBufferLength];
        reader.ReadBytes(bytes);
        return bytes;
    }

    public Task SendStartAsync() => WriteControlAsync(new byte[] { 0x01 });
    public Task SendStopAsync() => WriteControlAsync(new byte[] { 0x02 });
    public Task SendGetStatusAsync() => WriteControlAsync(new byte[] { 0x03 });

    private async Task WriteControlAsync(byte[] bytes)
    {
        if (_controlRx is null) throw new InvalidOperationException("BLE не подключен");
        using var writer = new DataWriter();
        writer.WriteBytes(bytes);
        var status = await _controlRx.WriteValueAsync(writer.DetachBuffer(), GattWriteOption.WriteWithoutResponse);
        if (status != GattCommunicationStatus.Success)
            throw new InvalidOperationException($"BLE write failed: {status}");
    }

    public async Task DisconnectAsync()
    {
        if (_liveTx is not null)
        {
            try { await _liveTx.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None); } catch { }
            _liveTx.ValueChanged -= LiveTxOnValueChanged;
        }
        if (_controlTx is not null) _controlTx.ValueChanged -= ControlTxOnValueChanged;
        if (_statusTx is not null) _statusTx.ValueChanged -= StatusTxOnValueChanged;

        if (_device is not null)
        {
            _device.ConnectionStatusChanged -= OnConnectionStatusChanged;
            _device.Dispose();
        }

        _device = null;
        _controlRx = null;
        _controlTx = null;
        _liveTx = null;
        _statusTx = null;
    }

    private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args)
    {
        StatusChanged?.Invoke(sender.ConnectionStatus == BluetoothConnectionStatus.Connected ? "BLE подключен" : "BLE отключен");
    }

    public void Dispose()
    {
        StopScan();
        DisconnectAsync().GetAwaiter().GetResult();
    }
}

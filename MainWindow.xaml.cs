using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using RowSenseWindows.Controls;
using RowSenseWindows.Models;
using RowSenseWindows.Services;

namespace RowSenseWindows;

public partial class MainWindow : Window
{
    private readonly BluetoothService _ble = new();
    private readonly TrainingLogger _logger = new();

    private readonly ObservableCollection<BleDeviceInfo> _devices = new();

    private readonly Dictionary<int, AthleteCard> _cards = new();

    private readonly Dictionary<int, CheckBox> _visibilityChecks = new();

    private SessionConfig _config = new();

    private bool _recording;

    public MainWindow()
    {
        InitializeComponent();

        DeviceCombo.ItemsSource = _devices;

        _ble.DeviceFound += d =>
            Dispatcher.Invoke(() =>
                _devices.Add(d));

        _ble.StatusChanged += s =>
            Dispatcher.Invoke(() =>
            {
                BleStatusText.Text = s;
                LiveBleStatus.Text = s;
            });

        _ble.StrokeReceived += s =>
            Dispatcher.Invoke(() =>
                HandleStroke(s));

        Closed += async (_, _) =>
            await _ble.DisconnectAsync();
    }

    private void ScanButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _devices.Clear();
        _ble.StartScan();
    }

    private async void ConnectButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (DeviceCombo.SelectedItem is not BleDeviceInfo device)
        {
            MessageBox.Show(
                "Сначала выбери найденный RowSense-Central.",
                "RowSense"
            );

            return;
        }

        ConnectButton.IsEnabled = false;

        try
        {
            bool ok =
                await _ble.ConnectAsync(device);

            if (!ok)
            {
                MessageBox.Show(
                    "BLE подключение не удалось. Проверь ESP и Bluetooth Windows.",
                    "RowSense"
                );
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "BLE error"
            );
        }
        finally
        {
            ConnectButton.IsEnabled = true;
        }
    }

    private void ContinueButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!_ble.IsConnected)
        {
            MessageBox.Show(
                "Сначала подключись к RowSense-Central по BLE.",
                "RowSense"
            );

            return;
        }

        if (!double.TryParse(
                ExternalLeverBox.Text.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var external)
            ||
            external < 150 ||
            external > 400)
        {
            MessageBox.Show(
                "Внешний рычаг должен быть примерно 150–400 см.",
                "RowSense"
            );

            return;
        }

        if (!double.TryParse(
                InternalLeverBox.Text.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var internalLever)
            ||
            internalLever < 50 ||
            internalLever > 160)
        {
            MessageBox.Show(
                "Внутренний рычаг должен быть примерно 50–160 см.",
                "RowSense"
            );

            return;
        }

        string boat =
            (BoatClassCombo.SelectedItem as ComboBoxItem)
            ?.Content
            ?.ToString()
            ?? "1x";

        _config = new SessionConfig
        {
            BoatClass = boat,
            ExternalLeverCm = external,
            InternalLeverCm = internalLever
        };

        SessionTitle.Text =
            $"{boat}   |   {external:0} / {internalLever:0} см";

        BuildAthleteUi();

        SetupPanel.Visibility =
            Visibility.Collapsed;

        LivePanel.Visibility =
            Visibility.Visible;
    }

    private void BuildAthleteUi()
    {
        AthleteGrid.Children.Clear();
        AthleteGrid.RowDefinitions.Clear();
        AthleteGrid.ColumnDefinitions.Clear();

        AthleteVisibilityPanel.Children.Clear();

        _cards.Clear();
        _visibilityChecks.Clear();

        for (int i = 1;
             i <= _config.AthleteCount;
             i++)
        {
            var check = new CheckBox
            {
                Content = $"Спортсмен {i}",
                IsChecked = true,
                Margin = new Thickness(
                    5,
                    0,
                    14,
                    0
                )
            };

            int n = i;

            check.Checked +=
                (_, _) =>
                    RebuildGridLayout();

            check.Unchecked +=
                (_, _) =>
                    RebuildGridLayout();

            AthleteVisibilityPanel
                .Children
                .Add(check);

            _visibilityChecks[n] = check;

            var card =
                new AthleteCard(n);

            _cards[n] = card;

            AthleteGrid
                .Children
                .Add(card);
        }

        RebuildGridLayout();
    }

    private void RebuildGridLayout()
    {
        if (_cards.Count == 0)
            return;

        AthleteGrid.RowDefinitions.Clear();
        AthleteGrid.ColumnDefinitions.Clear();

        var visible =
            _cards
                .Where(kv =>
                    _visibilityChecks[kv.Key]
                        .IsChecked == true)
                .Select(kv => kv.Value)
                .ToList();

        foreach (var card in _cards.Values)
        {
            card.Visibility =
                visible.Contains(card)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        int cols =
            visible.Count switch
            {
                <= 1 => 1,
                2 => 2,
                <= 4 => 2,
                _ => 4
            };

        int rows =
            Math.Max(
                1,
                (int)Math.Ceiling(
                    visible.Count /
                    (double)cols
                )
            );

        for (int c = 0;
             c < cols;
             c++)
        {
            AthleteGrid.ColumnDefinitions.Add(
                new ColumnDefinition()
            );
        }

        for (int r = 0;
             r < rows;
             r++)
        {
            AthleteGrid.RowDefinitions.Add(
                new RowDefinition
                {
                    MinHeight = 260
                });
        }

        for (int i = 0;
             i < visible.Count;
             i++)
        {
            Grid.SetColumn(
                visible[i],
                i % cols
            );

            Grid.SetRow(
                visible[i],
                i / cols
            );
        }
    }

    private async void StartButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            foreach (
                var card
                in _cards.Values)
            {
                card.ResetAverage();
            }

            _logger.Start(_config);

            await _ble.SendStartAsync();

            _recording = true;

            StartButton.IsEnabled = false;
            FinishButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "START error"
            );
        }
    }

    private async void FinishButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            await _ble.SendStopAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "STOP error"
            );
        }

        _recording = false;

        var file =
            _logger.Finish();

        StartButton.IsEnabled = true;
        FinishButton.IsEnabled = false;

        if (file is not null)
        {
            MessageBox.Show(
                $"Тренировка сохранена:\n{file}",
                "RowSense"
            );
        }
    }

    private void HandleStroke(
        StrokeData raw)
    {
        if (!_cards.TryGetValue(
                raw.AthleteNumber,
                out var card))
        {
            return;
        }

        var correctedForces =
            raw.Forces
                .Select(f =>
                    (float)(
                        f *
                        _config.ForceCorrection
                    ))
                .ToArray();

        float correctedPower =
            (float)(
                raw.Power *
                _config.PowerCorrection
            );

        card.UpdateStroke(
            correctedForces,
            correctedPower,
            raw.Tempo
        );

        if (_recording)
        {
            _logger.Add(
                new TrainingStroke
                {
                    TimestampUtc =
                        DateTime.UtcNow,

                    AthleteNumber =
                        raw.AthleteNumber,

                    Forces =
                        correctedForces,

                    Tempo =
                        raw.Tempo,

                    Power =
                        correctedPower,

                    StrokeTime =
                        raw.StrokeTime
                });
        }
    }

    private void BackButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_recording)
        {
            MessageBox.Show(
                "Сначала нажми FINISH.",
                "RowSense"
            );

            return;
        }

        LivePanel.Visibility =
            Visibility.Collapsed;

        SetupPanel.Visibility =
            Visibility.Visible;
    }

    private void TrainingsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Directory.CreateDirectory(
            TrainingLogger
                .TrainingsDirectory
        );

        Process.Start(
            new ProcessStartInfo
            {
                FileName =
                    TrainingLogger
                        .TrainingsDirectory,

                UseShellExecute = true
            });
    }
}

using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Line = System.Windows.Shapes.Line;
using Polyline = System.Windows.Shapes.Polyline;

namespace GrzmotoEditor;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<Entry> entries = new();
    private string? voicebankDirectory;
    private Entry? selected;
    private WaveformData? waveform;
    private int draggingBoundary = -1;

    public MainWindow()
    {
        InitializeComponent();
        RefreshList();
    }

    private void RefreshList()
    {
        var filter = SearchBox.Text.Trim();
        EntryList.ItemsSource = entries
            .Where(e => e.Alias.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        e.Audio.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private void Search_Changed(object sender, TextChangedEventArgs e) => RefreshList();

    private void EntryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        selected = EntryList.SelectedItem as Entry;
        if (selected is null)
        {
            ClearInspector();
            return;
        }

        AliasBox.Text = selected.Alias;
        AudioBox.Text = selected.Audio;
        TimingBox.Text = FormatTimings(selected.Timings);
        CurrentAlias.Text = selected.Alias;
        UpdateTimeline();
        LoadSelectedWaveform();
        ValidationText.Text = "";
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        entries.Clear();
        voicebankDirectory = null;
        selected = null;
        RefreshList();
        ClearInspector();
        StatusText.Text = "New configuration · select a WAV folder";
    }

    private void ClearInspector()
    {
        AliasBox.Clear();
        AudioBox.Clear();
        TimingBox.Clear();
        CurrentAlias.Text = "Select a phoneme entry";
        Timeline.ItemsSource = null;
        ValidationText.Text = "";
        waveform = null;
        WaveformStatus.Text = voicebankDirectory is null
            ? "Select a WAV folder, then select an entry."
            : "Select an entry to display its waveform.";
        DrawWaveform();
    }

    private void SelectWavFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select a folder containing WAV files",
            InitialDirectory = voicebankDirectory
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var folder = dialog.FolderName;
            ValidateFolderContents(folder);
            var configPath = Path.Combine(folder, "grzmoto.json");
            var loadedEntries = File.Exists(configPath)
                ? LoadEntries(configPath, folder)
                : new List<Entry>();

            voicebankDirectory = folder;
            entries.Clear();
            foreach (var entry in loadedEntries)
                entries.Add(entry);
            selected = null;
            RefreshList();
            ClearInspector();
            StatusText.Text = File.Exists(configPath)
                ? $"Loaded {entries.Count} entries · {Path.GetFileName(folder)}"
                : $"WAV folder selected · {Path.GetFileName(folder)}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                   InvalidDataException or FormatException or JsonException or
                                   InvalidOperationException or NotSupportedException)
        {
            MessageBox.Show(ex.Message, "Unable to use WAV folder", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Grzmoto configuration (grzmoto.json)|grzmoto.json|JSON files (*.json)|*.json"
        };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            if (!string.Equals(Path.GetFileName(dialog.FileName), "grzmoto.json", StringComparison.OrdinalIgnoreCase))
                throw new FormatException("The configuration file must be named grzmoto.json.");

            var folder = Path.GetDirectoryName(dialog.FileName)
                ?? throw new FormatException("The configuration folder could not be determined.");
            ValidateFolderContents(folder);
            var loadedEntries = LoadEntries(dialog.FileName, folder);

            voicebankDirectory = folder;
            entries.Clear();
            foreach (var entry in loadedEntries)
                entries.Add(entry);
            selected = null;
            ClearInspector();
            RefreshList();
            StatusText.Text = $"Loaded {entries.Count} entries · {Path.GetFileName(folder)}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                   InvalidDataException or FormatException or JsonException or
                                   InvalidOperationException or NotSupportedException)
        {
            MessageBox.Show(ex.Message, "Unable to open", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static List<Entry> LoadEntries(string configPath, string folder)
    {
        var root = JsonNode.Parse(File.ReadAllText(configPath)) as JsonObject
            ?? throw new FormatException("Root must be a JSON object.");
        var loaded = new List<Entry>();
        foreach (var item in root)
        {
            if (item.Value is not JsonArray arr || arr.Count < 2)
                throw new FormatException($"Invalid entry: {item.Key}");

            var audio = arr[0]?.GetValue<string>()
                ?? throw new FormatException($"Missing audio filename for '{item.Key}'.");
            var timings = arr.Skip(1)
                .Select(x => x?.GetValue<double>() ?? throw new FormatException("Invalid timestamp."))
                .ToList();
            var entry = new Entry(item.Key, audio, timings);
            Validate(entry);
            ValidateAudioInFolder(entry, folder);
            loaded.Add(entry);
        }

        return loaded;
    }

    private static void ValidateFolderContents(string folder)
    {
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException($"The selected folder does not exist: {folder}");

        var configCount = 0;
        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(file);
            if (string.Equals(name, "grzmoto.json", StringComparison.OrdinalIgnoreCase))
            {
                if (++configCount > 1)
                    throw new FormatException("The voicebank folder may contain only one grzmoto.json file.");
                continue;
            }
            if (string.Equals(Path.GetExtension(name), ".wav", StringComparison.OrdinalIgnoreCase))
                continue;

            throw new FormatException(
                $"'{name}' is not allowed in the voicebank folder. It may contain only WAV files and grzmoto.json.");
        }
    }

    private static void ValidateAudioInFolder(Entry entry, string folder)
    {
        if (!File.Exists(Path.Combine(folder, entry.Audio)))
            throw new FileNotFoundException($"WAV file '{entry.Audio}' was not found in the selected folder.");
    }

    private static void Validate(Entry entry)
    {
        var phonemes = entry.Alias.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (phonemes.Length == 0 || string.IsNullOrWhiteSpace(entry.Audio))
            throw new FormatException("Alias and audio filename are required.");
        if (!string.Equals(Path.GetFileName(entry.Audio), entry.Audio, StringComparison.Ordinal) ||
            !string.Equals(Path.GetExtension(entry.Audio), ".wav", StringComparison.OrdinalIgnoreCase))
            throw new FormatException("Audio must be a WAV filename in the selected folder.");
        if (entry.Timings.Count != phonemes.Length + 1)
            throw new FormatException($"'{entry.Alias}' requires {phonemes.Length + 1} boundaries, got {entry.Timings.Count}.");
        if (entry.Timings.Any(t => !double.IsFinite(t) || t < 0) ||
            entry.Timings.Zip(entry.Timings.Skip(1)).Any(pair => pair.First >= pair.Second))
            throw new FormatException("Boundaries must be finite, nonnegative and strictly increasing.");
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var alias = "new";
        var index = 2;
        while (entries.Any(x => x.Alias == alias))
            alias = $"new{index++}";

        var entry = new Entry(alias, "sample.wav", new List<double> { 0, 500 });
        entries.Add(entry);
        RefreshList();
        EntryList.SelectedItem = entry;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (selected is null)
            return;

        try
        {
            var timings = ParseTimings(TimingBox.Text);
            var candidate = new Entry(AliasBox.Text.Trim(), AudioBox.Text.Trim(), timings);
            Validate(candidate);
            if (voicebankDirectory is not null)
                ValidateAudioInFolder(candidate, voicebankDirectory);
            if (entries.Any(e => e != selected && e.Alias == candidate.Alias))
                throw new FormatException("This alias already exists.");

            var updatedEntry = selected;
            updatedEntry.Alias = candidate.Alias;
            updatedEntry.Audio = candidate.Audio;
            updatedEntry.Timings = candidate.Timings;
            RefreshList();
            selected = updatedEntry;
            EntryList.SelectedItem = updatedEntry;
            selected = updatedEntry;
            AliasBox.Text = updatedEntry.Alias;
            AudioBox.Text = updatedEntry.Audio;
            TimingBox.Text = FormatTimings(updatedEntry.Timings);
            CurrentAlias.Text = updatedEntry.Alias;
            UpdateTimeline();
            LoadSelectedWaveform();
            ValidationText.Text = "Changes applied.";
            StatusText.Text = "Unsaved changes";
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or FileNotFoundException or
                                   UnauthorizedAccessException or IOException)
        {
            ValidationText.Text = ex.Message;
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (selected is null)
            return;

        entries.Remove(selected);
        selected = null;
        RefreshList();
        ClearInspector();
        StatusText.Text = "Unsaved changes";
    }

    private static List<double> ParseTimings(string text)
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        var delimiter = text.Contains(';') ? ';' : ',';
        return text.Split(delimiter, StringSplitOptions.TrimEntries)
            .Select(value =>
            {
                if (double.TryParse(value, System.Globalization.NumberStyles.Float, culture, out var timing) ||
                    double.TryParse(value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out timing))
                    return timing;

                throw new FormatException($"'{value}' is not a valid timing value.");
            })
            .ToList();
    }

    private static string FormatTimings(IEnumerable<double> timings) =>
        string.Join("; ", timings.Select(timing =>
            timing.ToString("G", System.Globalization.CultureInfo.CurrentCulture)));

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (voicebankDirectory is null)
        {
            MessageBox.Show("Select a WAV folder before saving.", "No WAV folder selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            ValidateFolderContents(voicebankDirectory);
            foreach (var entry in entries)
            {
                Validate(entry);
                ValidateAudioInFolder(entry, voicebankDirectory);
            }

            var configPath = Path.Combine(voicebankDirectory, "grzmoto.json");
            var data = new Dictionary<string, object[]>();
            foreach (var entry in entries)
                data.Add(entry.Alias, new object[] { entry.Audio }.Concat(entry.Timings.Cast<object>()).ToArray());

            File.WriteAllText(configPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            StatusText.Text = $"Saved · {Path.GetFileName(voicebankDirectory)}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                   InvalidDataException or FormatException or NotSupportedException)
        {
            MessageBox.Show(ex.Message, "Unable to save", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateTimeline()
    {
        if (selected is null)
        {
            Timeline.ItemsSource = null;
            return;
        }

        Timeline.ItemsSource = selected.Alias
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select((phoneme, index) => $"{phoneme} · {selected.Timings[index]:0.##} ms")
            .ToArray();
    }

    private void LoadSelectedWaveform()
    {
        waveform = null;
        if (selected is null || voicebankDirectory is null)
        {
            WaveformStatus.Text = voicebankDirectory is null
                ? "Select a WAV folder, then select an entry."
                : "Select an entry to display its waveform.";
            DrawWaveform();
            return;
        }

        var audioPath = Path.Combine(voicebankDirectory, selected.Audio);
        try
        {
            waveform = ReadWaveform(audioPath);
            WaveformStatus.Text = $"{selected.Audio} · {waveform.DurationMilliseconds:0.##} ms";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                   InvalidDataException or NotSupportedException)
        {
            WaveformStatus.Text = $"Unable to display {selected.Audio}: {ex.Message}";
        }

        DrawWaveform();
    }

    private void WaveformCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawWaveform();

    private void WaveformCanvas_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (selected is null || waveform is null || waveform.DurationMilliseconds <= 0 ||
            WaveformCanvas.ActualWidth <= 0)
            return;

        draggingBoundary = -1;
        var pointerX = e.GetPosition(WaveformCanvas).X;
        var closestDistance = 12d;
        for (var i = 0; i < selected.Timings.Count; i++)
        {
            var markerX = Math.Clamp(
                selected.Timings[i] / waveform.DurationMilliseconds * WaveformCanvas.ActualWidth,
                0,
                WaveformCanvas.ActualWidth);
            var distance = Math.Abs(pointerX - markerX);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                draggingBoundary = i;
            }
        }

        if (draggingBoundary < 0)
            return;

        WaveformCanvas.CaptureMouse();
        WaveformCanvas.Cursor = System.Windows.Input.Cursors.SizeWE;
        e.Handled = true;
    }

    private void WaveformCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (draggingBoundary < 0 || !WaveformCanvas.IsMouseCaptured ||
            selected is null || waveform is null || waveform.DurationMilliseconds <= 0)
            return;

        var pointerX = Math.Clamp(e.GetPosition(WaveformCanvas).X, 0, WaveformCanvas.ActualWidth);
        var newTiming = pointerX / WaveformCanvas.ActualWidth * waveform.DurationMilliseconds;
        var lower = draggingBoundary == 0 ? 0 : selected.Timings[draggingBoundary - 1];
        var upper = draggingBoundary == selected.Timings.Count - 1
            ? waveform.DurationMilliseconds
            : selected.Timings[draggingBoundary + 1];
        var gap = upper - lower;
        if (gap <= 0)
            return;

        var minimumGap = Math.Min(0.01, gap / 4);
        selected.Timings[draggingBoundary] = Math.Clamp(
            newTiming,
            lower + minimumGap,
            upper - minimumGap);
        TimingBox.Text = FormatTimings(selected.Timings);
        UpdateTimeline();
        DrawWaveform();
        StatusText.Text = "Unsaved changes";
    }

    private void WaveformCanvas_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (draggingBoundary < 0)
            return;

        draggingBoundary = -1;
        WaveformCanvas.Cursor = System.Windows.Input.Cursors.Arrow;
        WaveformCanvas.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void DrawWaveform()
    {
        if (WaveformCanvas is null)
            return;

        WaveformCanvas.Children.Clear();
        var width = WaveformCanvas.ActualWidth;
        var height = WaveformCanvas.ActualHeight;
        if (width <= 1 || height <= 1 || waveform is null)
            return;

        var center = height / 2;
        var amplitude = Math.Max(1, height * 0.42);
        WaveformCanvas.Children.Add(new Line
        {
            X1 = 0,
            X2 = width,
            Y1 = center,
            Y2 = center,
            Stroke = new SolidColorBrush(Color.FromRgb(55, 55, 76)),
            StrokeThickness = 1
        });

        var upper = new PointCollection(waveform.Maximum.Length);
        var lower = new PointCollection(waveform.Minimum.Length);
        for (var i = 0; i < waveform.Maximum.Length; i++)
        {
            var x = waveform.Maximum.Length == 1 ? width / 2 : i * width / (waveform.Maximum.Length - 1);
            upper.Add(new Point(x, center - waveform.Maximum[i] * amplitude));
            lower.Add(new Point(x, center - waveform.Minimum[i] * amplitude));
        }

        WaveformCanvas.Children.Add(new Polyline
        {
            Points = upper,
            Stroke = new SolidColorBrush(Color.FromRgb(186, 153, 255)),
            StrokeThickness = 1
        });
        WaveformCanvas.Children.Add(new Polyline
        {
            Points = lower,
            Stroke = new SolidColorBrush(Color.FromRgb(186, 153, 255)),
            StrokeThickness = 1
        });

        if (selected is null)
            return;

        var duration = waveform.DurationMilliseconds;
        for (var i = 0; i < selected.Timings.Count; i++)
        {
            var x = selected.Timings[i] / duration * width;
            if (x < 0 || x > width)
                continue;

            var markerColor = i == 0 || i == selected.Timings.Count - 1
                ? Color.FromRgb(255, 190, 110)
                : Color.FromRgb(127, 221, 193);
            var brush = new SolidColorBrush(markerColor);
            WaveformCanvas.Children.Add(new Line
            {
                X1 = x,
                X2 = x,
                Y1 = 0,
                Y2 = height,
                Stroke = brush,
                StrokeThickness = 1
            });
            var label = new TextBlock
            {
                Text = $"{selected.Timings[i]:0.##} ms",
                Foreground = brush,
                FontSize = 10
            };
            WaveformCanvas.Children.Add(label);
            Canvas.SetLeft(label, Math.Clamp(x + 3, 0, Math.Max(0, width - 58)));
            Canvas.SetTop(label, 3 + (i % 2) * 14);
        }
    }

    private static WaveformData ReadWaveform(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        if (stream.Length < 12 || ReadFourCc(reader) != "RIFF")
            throw new InvalidDataException("This is not a RIFF WAV file.");

        var riffSize = reader.ReadUInt32();
        if (ReadFourCc(reader) != "WAVE")
            throw new InvalidDataException("The file does not contain WAVE audio.");
        var riffEnd = Math.Min(stream.Length, 8L + riffSize);
        ushort format = 0;
        ushort channels = 0;
        ushort bitsPerSample = 0;
        ushort blockAlign = 0;
        uint sampleRate = 0;
        long dataOffset = -1;
        uint dataSize = 0;

        while (stream.Position + 8 <= riffEnd)
        {
            var chunkId = ReadFourCc(reader);
            var chunkSize = reader.ReadUInt32();
            var chunkStart = stream.Position;
            if (chunkSize > riffEnd - chunkStart)
                throw new InvalidDataException($"The WAV '{chunkId}' chunk is truncated.");

            if (chunkId == "fmt ")
            {
                if (chunkSize < 16)
                    throw new InvalidDataException("The WAV format chunk is incomplete.");
                format = reader.ReadUInt16();
                channels = reader.ReadUInt16();
                sampleRate = reader.ReadUInt32();
                reader.ReadUInt32();
                blockAlign = reader.ReadUInt16();
                bitsPerSample = reader.ReadUInt16();

                if (format == 0xFFFE)
                {
                    if (chunkSize < 40)
                        throw new InvalidDataException("The extensible WAV format chunk is incomplete.");
                    stream.Position = chunkStart;
                    var formatData = reader.ReadBytes(40);
                    var subFormat = new Guid(formatData.AsSpan(24, 16));
                    if (subFormat == new Guid("00000001-0000-0010-8000-00aa00389b71"))
                        format = 1;
                    else if (subFormat == new Guid("00000003-0000-0010-8000-00aa00389b71"))
                        format = 3;
                    else
                        throw new NotSupportedException("This WAV uses an unsupported extensible audio format.");
                }
            }
            else if (chunkId == "data")
            {
                dataOffset = chunkStart;
                dataSize = chunkSize;
            }

            stream.Position = chunkStart + chunkSize + (chunkSize & 1);
        }

        if (format is not (1 or 3) || channels == 0 || sampleRate == 0 ||
            blockAlign == 0 || dataOffset < 0)
            throw new InvalidDataException("The WAV is missing valid format or audio data.");
        if (channels > 32 || blockAlign > 4096)
            throw new NotSupportedException("The WAV has an unsupported channel layout.");
        if (format == 1 && bitsPerSample is not (8 or 16 or 24 or 32) ||
            format == 3 && bitsPerSample is not (32 or 64))
            throw new NotSupportedException($"WAV audio with {bitsPerSample}-bit samples is not supported.");

        var bytesPerSample = bitsPerSample / 8;
        if (blockAlign < channels * bytesPerSample)
            throw new InvalidDataException("The WAV frame size is invalid.");

        var frameCount = dataSize / blockAlign;
        if (frameCount == 0)
            throw new InvalidDataException("The WAV contains no audio samples.");

        var bucketCount = (int)Math.Min(1600, frameCount);
        var minimum = Enumerable.Repeat(1d, bucketCount).ToArray();
        var maximum = Enumerable.Repeat(-1d, bucketCount).ToArray();
        var framesPerRead = Math.Max(1, 65536 / blockAlign);
        var buffer = new byte[framesPerRead * blockAlign];
        stream.Position = dataOffset;
        long frameIndex = 0;

        while (frameIndex < frameCount)
        {
            var framesToRead = (int)Math.Min(framesPerRead, frameCount - frameIndex);
            var bytesToRead = framesToRead * blockAlign;
            var bytesRead = 0;
            while (bytesRead < bytesToRead)
            {
                var read = stream.Read(buffer, bytesRead, bytesToRead - bytesRead);
                if (read == 0)
                    throw new InvalidDataException("The WAV audio data is truncated.");
                bytesRead += read;
            }

            for (var frame = 0; frame < framesToRead; frame++)
            {
                var frameOffset = frame * blockAlign;
                var frameMinimum = 1d;
                var frameMaximum = -1d;
                for (var channel = 0; channel < channels; channel++)
                {
                    var sample = ReadSample(buffer, frameOffset + channel * bytesPerSample, format, bitsPerSample);
                    frameMinimum = Math.Min(frameMinimum, sample);
                    frameMaximum = Math.Max(frameMaximum, sample);
                }

                var bucket = (int)(frameIndex * bucketCount / frameCount);
                minimum[bucket] = Math.Min(minimum[bucket], frameMinimum);
                maximum[bucket] = Math.Max(maximum[bucket], frameMaximum);
                frameIndex++;
            }
        }

        return new WaveformData(minimum, maximum, frameCount * 1000d / sampleRate);
    }

    private static string ReadFourCc(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        if (bytes.Length != 4)
            throw new InvalidDataException("The WAV file is truncated.");
        return Encoding.ASCII.GetString(bytes);
    }

    private static double ReadSample(byte[] data, int offset, ushort format, ushort bitsPerSample)
    {
        if (format == 3)
        {
            var floatingPoint = bitsPerSample == 32
                ? BitConverter.ToSingle(data, offset)
                : BitConverter.ToDouble(data, offset);
            return double.IsFinite(floatingPoint) ? Math.Clamp(floatingPoint, -1, 1) : 0;
        }

        return bitsPerSample switch
        {
            8 => (data[offset] - 128) / 128d,
            16 => BitConverter.ToInt16(data, offset) / 32768d,
            24 => ReadInt24(data, offset) / 8388608d,
            32 => BitConverter.ToInt32(data, offset) / 2147483648d,
            _ => throw new NotSupportedException($"WAV audio with {bitsPerSample}-bit samples is not supported.")
        };
    }

    private static int ReadInt24(byte[] data, int offset)
    {
        var value = data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16;
        return (value & 0x800000) == 0 ? value : value | unchecked((int)0xFF000000);
    }

    private sealed record WaveformData(double[] Minimum, double[] Maximum, double DurationMilliseconds);

    private sealed class Entry(string alias, string audio, List<double> timings)
    {
        public string Alias { get; set; } = alias;
        public string Audio { get; set; } = audio;
        public List<double> Timings { get; set; } = timings;
    }
}

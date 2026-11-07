using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace GrzmotoEditor;
public partial class MainWindow : Window
{
    private readonly ObservableCollection<Entry> entries = new();
    private string? path;
    private Entry? selected;
    public MainWindow() { InitializeComponent(); RefreshList(); }
    private void RefreshList()
    {
        var filter = SearchBox.Text.Trim();
        EntryList.ItemsSource = entries.Where(e => e.Alias.Contains(filter, StringComparison.OrdinalIgnoreCase) || e.Audio.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
    }
    private void Search_Changed(object sender, TextChangedEventArgs e) => RefreshList();
    private void EntryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        selected = EntryList.SelectedItem as Entry;
        if (selected is null) return;
        AliasBox.Text = selected.Alias; AudioBox.Text = selected.Audio;
        TimingBox.Text = string.Join(", ", selected.Timings);
        CurrentAlias.Text = selected.Alias;
        Timeline.ItemsSource = selected.Alias.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select((p, i) => $"{p}  ·  {selected.Timings.ElementAtOrDefault(i)} ms").ToArray();
        ValidationText.Text = "";
    }
    private void New_Click(object sender, RoutedEventArgs e)
    { entries.Clear(); path = null; selected = null; RefreshList(); ClearInspector(); StatusText.Text = "New voicebank configuration"; }
    private void ClearInspector() { AliasBox.Clear(); AudioBox.Clear(); TimingBox.Clear(); CurrentAlias.Text = "Select a phoneme entry"; Timeline.ItemsSource = null; ValidationText.Text = ""; }
    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Grzmoto JSON (*.json)|*.json|All files (*.*)|*.*" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var root = JsonNode.Parse(File.ReadAllText(dialog.FileName)) as JsonObject ?? throw new FormatException("Root must be a JSON object.");
            var loaded = new List<Entry>();
            foreach (var item in root)
            {
                if (item.Value is not JsonArray arr || arr.Count < 2) throw new FormatException($"Invalid entry: {item.Key}");
                var audio = arr[0]?.GetValue<string>() ?? throw new FormatException("Missing audio filename.");
                var times = arr.Skip(1).Select(x => x?.GetValue<double>() ?? throw new FormatException("Invalid timestamp.")).ToList();
                var entry = new Entry(item.Key, audio, times);
                Validate(entry); loaded.Add(entry);
            }
            entries.Clear(); foreach (var item in loaded) entries.Add(item);
            path = dialog.FileName; selected = null; ClearInspector(); RefreshList();
            StatusText.Text = $"Loaded {entries.Count} entries · {Path.GetFileName(path)}";
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Unable to open", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private static void Validate(Entry e)
    {
        var phonemes = e.Alias.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (phonemes.Length == 0 || string.IsNullOrWhiteSpace(e.Audio)) throw new FormatException("Alias and audio filename are required.");
        if (e.Timings.Count != phonemes.Length + 1) throw new FormatException($"'{e.Alias}' requires {phonemes.Length + 1} boundaries, got {e.Timings.Count}.");
        if (e.Timings.Any(t => !double.IsFinite(t) || t < 0) || e.Timings.Zip(e.Timings.Skip(1)).Any(pair => pair.First >= pair.Second))
            throw new FormatException("Boundaries must be finite, nonnegative and strictly increasing.");
    }
    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var alias = "new"; var index = 2;
        while (entries.Any(x => x.Alias == alias)) alias = $"new{index++}";
        var entry = new Entry(alias, "sample.wav", new List<double> { 0, 500 });
        entries.Add(entry); RefreshList(); EntryList.SelectedItem = entry;
    }
    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (selected is null) return;
        try
        {
            var times = TimingBox.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(s => double.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToList();
            var candidate = new Entry(AliasBox.Text.Trim(), AudioBox.Text.Trim(), times);
            Validate(candidate);
            if (entries.Any(e => e != selected && e.Alias == candidate.Alias)) throw new FormatException("This alias already exists.");
            selected.Alias = candidate.Alias; selected.Audio = candidate.Audio; selected.Timings = candidate.Timings;
            RefreshList(); EntryList.SelectedItem = selected;
            CurrentAlias.Text = selected.Alias;
            Timeline.ItemsSource = selected.Alias.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select((p, i) => $"{p}  ·  {selected.Timings[i]} ms").ToArray();
            ValidationText.Text = "Changes applied."; StatusText.Text = "Unsaved changes";
        }
        catch (Exception ex) { ValidationText.Text = ex.Message; }
    }
    private void Delete_Click(object sender, RoutedEventArgs e)
    { if (selected is null) return; entries.Remove(selected); selected = null; RefreshList(); ClearInspector(); StatusText.Text = "Unsaved changes"; }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            foreach (var entry in entries) Validate(entry);
            if (path is null)
            {
                var dialog = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "grzmoto.json" };
                if (dialog.ShowDialog() != true) return;
                path = dialog.FileName;
            }
            var data = new Dictionary<string, object[]>();
            foreach (var entry in entries) data.Add(entry.Alias, new object[] { entry.Audio }.Concat(entry.Timings.Cast<object>()).ToArray());
            File.WriteAllText(path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            StatusText.Text = $"Saved · {Path.GetFileName(path)}";
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Unable to save", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private sealed class Entry(string alias, string audio, List<double> timings)
    { public string Alias { get; set; } = alias; public string Audio { get; set; } = audio; public List<double> Timings { get; set; } = timings; }
}

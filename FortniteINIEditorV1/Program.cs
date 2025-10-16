

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace FortniteIniEditor
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new IniEditorForm());
        }
    }

    public class IniEditorForm : Form
    {
        private readonly TextBox _pathBox = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
        private readonly Button _detectBtn = new Button { Text = "Detect", Width = 80 };
        private readonly Button _browseBtn = new Button { Text = "Browse", Width = 80 };
        private readonly Button _loadBtn = new Button { Text = "Load", Width = 80 };
        private readonly Button _backupBtn = new Button { Text = "Backup", Width = 80 };
        private readonly Button _saveBtn = new Button { Text = "Save", Width = 80 };
        private readonly ComboBox _fileCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
        private readonly TextBox _searchBox = new TextBox(); // PlaceholderText is .NET 6+ only; keep generic for compatibility
        private readonly DataGridView _grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = true, AllowUserToDeleteRows = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        private readonly StatusStrip _status = new StatusStrip();
        private readonly ToolStripStatusLabel _statusLabel = new ToolStripStatusLabel();
        private readonly ToolStripDropDownButton _presetsDrop = new ToolStripDropDownButton("Presets");
        private readonly ToolStripDropDownButton _toolsDrop = new ToolStripDropDownButton("Tools");
        private IniDocument _doc = new IniDocument();
        private string _loadedPath = string.Empty;

        public IniEditorForm()
        {
            Text = "Fortnite INI Editor by CEZEY";
            Width = 1100; Height = 720;
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); this.ShowIcon = true; } catch { }

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(8), WrapContents = false };

            _fileCombo.Items.AddRange(new object[] { "GameUserSettings.ini", "Engine.ini", "Input.ini" });
            _fileCombo.SelectedIndex = 0;

            _pathBox.Width = 500;

            top.Controls.Add(new Label { Text = "File:", AutoSize = true, Margin = new Padding(0, 10, 6, 0) });
            top.Controls.Add(_fileCombo);
            top.Controls.Add(new Label { Text = "Path:", AutoSize = true, Margin = new Padding(16, 10, 6, 0) });
            top.Controls.Add(_pathBox);
            top.Controls.Add(_detectBtn);
            top.Controls.Add(_browseBtn);
            top.Controls.Add(_loadBtn);
            top.Controls.Add(_backupBtn);
            top.Controls.Add(_saveBtn);
            top.Controls.Add(new Label { Text = "Search:", AutoSize = true, Margin = new Padding(16, 10, 6, 0) });
            _searchBox.Width = 200; top.Controls.Add(_searchBox);

            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Section", DataPropertyName = "Section", MinimumWidth = 120 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Key", DataPropertyName = "Key", MinimumWidth = 160 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Value", DataPropertyName = "Value", MinimumWidth = 240 });

            var cms = new ContextMenuStrip();
            cms.Items.Add("Add row").Click += (_, __) => _grid.Rows.Add();
            cms.Items.Add("Duplicate row").Click += (_, __) => DuplicateSelectedRow();
            cms.Items.Add("Delete row").Click += (_, __) => DeleteSelectedRow();
            _grid.ContextMenuStrip = cms;

            _status.Items.Add(_statusLabel);
            _status.Items.Add(new ToolStripStatusLabel { Spring = true });
            _status.Items.Add(_presetsDrop);
            _status.Items.Add(_toolsDrop);

            Controls.Add(_grid);
            Controls.Add(top);
            Controls.Add(_status);

            // Wire events
            _detectBtn.Click += (_, __) => DetectPath();
            _browseBtn.Click += (_, __) => BrowsePath();
            _loadBtn.Click += (_, __) => LoadIni();
            _saveBtn.Click += (_, __) => SaveIni();
            _backupBtn.Click += (_, __) => Backup();
            _fileCombo.SelectedIndexChanged += (_, __) => UpdateSuggestedPath();
            _searchBox.TextChanged += (_, __) => ApplyFilter();

            // Presets & Tools
            BuildPresetMenu();
            BuildToolsMenu();

            UpdateSuggestedPath();
            _statusLabel.Text = "Ready";
        }

        private void DuplicateSelectedRow()
        {
            if (_grid.CurrentRow == null) return;
            var idx = _grid.CurrentRow.Index;
            var cells = _grid.Rows[idx].Cells;
            _grid.Rows.Add(cells[0].Value, cells[1].Value, cells[2].Value);
        }

        private void DeleteSelectedRow()
        {
            if (_grid.CurrentRow != null && !_grid.CurrentRow.IsNewRow) _grid.Rows.RemoveAt(_grid.CurrentRow.Index);
        }

        private void BuildPresetMenu()
        {
            _presetsDrop.DropDownItems.Clear();

            void Add(string title, Action action, string tooltip = null)
            {
                var item = new ToolStripMenuItem(title);
                if (!string.IsNullOrWhiteSpace(tooltip)) item.ToolTipText = tooltip;
                item.Click += (_, __) => { action(); ApplyFilter(); };
                _presetsDrop.DropDownItems.Add(item);
            }

            Add("Performance: 1080p Fullscreen", () => ApplyPairs(new[]
            {
                P("/Script/FortniteGame.FortGameUserSettings","ResolutionSizeX","1920"),
                P("/Script/FortniteGame.FortGameUserSettings","ResolutionSizeY","1080"),
                P("/Script/FortniteGame.FortGameUserSettings","bUseVSync","False"),
                P("/Script/FortniteGame.FortGameUserSettings","FrameRateLimit","240.000000"),
                P("/Script/FortniteGame.FortGameUserSettings","FullscreenMode","0"), // 0=Fullscreen,1=WindowedFullscreen,2=Windowed
            }));

            Add("Performance: 1600x900 Fullscreen", () => ApplyPairs(new[]
            {
                P("/Script/FortniteGame.FortGameUserSettings","ResolutionSizeX","1600"),
                P("/Script/FortniteGame.FortGameUserSettings","ResolutionSizeY","900"),
                P("/Script/FortniteGame.FortGameUserSettings","bUseVSync","False"),
                P("/Script/FortniteGame.FortGameUserSettings","FrameRateLimit","240.000000"),
                P("/Script/FortniteGame.FortGameUserSettings","FullscreenMode","0"),
            }));

            Add("Scalability: Low", () => ApplyPairs(ScalabilityPreset(0)));
            Add("Scalability: Medium", () => ApplyPairs(ScalabilityPreset(1)));
            Add("Scalability: High", () => ApplyPairs(ScalabilityPreset(2)));
            Add("Scalability: Epic", () => ApplyPairs(ScalabilityPreset(3)));

            Add("Input: RawMouseInput On", () => ApplyPairs(new[] { P("/Script/FortniteGame.FortGameUserSettings", "bUseHardwareCursor", "False"), }));

            Add("Networking: DisableReplayRecording", () => ApplyPairs(new[] { P("/Script/FortniteGame.FortGameUserSettings", "bDisableReplayRecording", "True"), }));

            _presetsDrop.DropDownItems.Add(new ToolStripSeparator());
            Add("Import preset JSON...", ImportPresetJson, "Load community/shared presets from a .json file");
        }

        private void BuildToolsMenu()
        {
            _toolsDrop.DropDownItems.Clear();

            var exportItem = new ToolStripMenuItem("Export current as preset JSON...");
            exportItem.Click += (_, __) => ExportPresetJson();
            _toolsDrop.DropDownItems.Add(exportItem);

            var normalizeItem = new ToolStripMenuItem("Normalize keys (trim/unique)");
            normalizeItem.Click += (_, __) => { NormalizeGrid(); };
            _toolsDrop.DropDownItems.Add(normalizeItem);

            var openFolderItem = new ToolStripMenuItem("Open file location");
            openFolderItem.Click += (_, __) =>
            {
                if (File.Exists(_loadedPath)) System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + _loadedPath + "\"");
                else MessageBox.Show("No file loaded yet.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            _toolsDrop.DropDownItems.Add(openFolderItem);

            _toolsDrop.DropDownItems.Add(new ToolStripSeparator());
            var aboutItem = new ToolStripMenuItem("About…");
            aboutItem.Click += (_, __) => new AboutForm().ShowDialog(this);
            _toolsDrop.DropDownItems.Add(aboutItem);
        }

        private static IniPair P(string section, string key, string value) => new IniPair(section, key, value);

        private static IEnumerable<IniPair> ScalabilityPreset(int level)
        {
            var s = "/Script/FortniteGame.FortGameUserSettings"; // 0=Low 1=Medium 2=High 3=Epic
            return new[]
            {
                P(s,"sg.ViewDistance", level.ToString()),
                P(s,"sg.AntiAliasing", level.ToString()),
                P(s,"sg.ShadowQuality", level.ToString()),
                P(s,"sg.PostProcessQuality", level.ToString()),
                P(s,"sg.TextureQuality", level.ToString()),
                P(s,"sg.EffectsQuality", level.ToString()),
                P(s,"sg.FoliageQuality", level.ToString()),
                P(s,"sg.GlobalIlluminationQuality", level.ToString()),
                P(s,"sg.ReflectionQuality", level.ToString()),
            };
        }

        private void ApplyPairs(IEnumerable<IniPair> pairs)
        {
            foreach (var p in pairs) EnsureRow(p.Section, p.Key, p.Value);
            _statusLabel.Text = "Preset applied (not saved yet).";
        }

        private void NormalizeGrid()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var toRemove = new List<DataGridViewRow>();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow) continue;
                var sec = (row.Cells[0].Value?.ToString() ?? string.Empty).Trim();
                var key = (row.Cells[1].Value?.ToString() ?? string.Empty).Trim();
                var val = (row.Cells[2].Value?.ToString() ?? string.Empty).Trim();
                row.Cells[0].Value = sec; row.Cells[1].Value = key; row.Cells[2].Value = val;
                var sig = sec + "\u0001" + key;
                if (!seen.Add(sig)) toRemove.Add(row);
            }
            foreach (var r in toRemove) _grid.Rows.Remove(r);
            _statusLabel.Text = $"Normalized. Removed {toRemove.Count} duplicate rows.";
        }

        private void ImportPresetJson()
        {
            using (var ofd = new OpenFileDialog { Filter = "Preset JSON (*.json)|*.json|All files (*.*)|*.*" })
            {
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var json = File.ReadAllText(ofd.FileName);
                    var list = PresetJson.Parse(json);
                    if (list.Count == 0) { MessageBox.Show("No entries found in JSON.", "Import", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                    ApplyPairs(list);
                    MessageBox.Show($"Imported {list.Count} entries.", "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to import: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ExportPresetJson()
        {
            using (var sfd = new SaveFileDialog { Filter = "Preset JSON (*.json)|*.json", FileName = "preset.json" })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                var list = EnumerateGrid().ToList();
                File.WriteAllText(sfd.FileName, PresetJson.Serialize(list));
                MessageBox.Show("Exported current grid to JSON.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void DetectPath()
        {
            try
            {
                var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string platform = "WindowsClient";
                var candidate = Path.Combine(baseDir, "FortniteGame", "Saved", "Config", platform, _fileCombo.SelectedItem.ToString());
                _pathBox.Text = candidate;
                _statusLabel.Text = Directory.Exists(Path.GetDirectoryName(candidate)) ? "Detected default path." : "Default path guessed; folder not found.";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Detect failed: " + ex.Message;
            }
        }

        private void UpdateSuggestedPath() => DetectPath();

        private void BrowsePath()
        {
            using (var ofd = new OpenFileDialog { Filter = "INI files (*.ini)|*.ini|All files (*.*)|*.*" })
            {
                if (ofd.ShowDialog(this) == DialogResult.OK) _pathBox.Text = ofd.FileName;
            }
        }

        private void LoadIni()
        {
            try
            {
                var p = _pathBox.Text.Trim();
                if (!File.Exists(p)) { MessageBox.Show("File not found.", "Load", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                _doc = IniDocument.LoadFromFile(p);
                _loadedPath = p;
                PopulateGrid(_doc);
                _statusLabel.Text = $"Loaded {_doc.Count} entries from file.";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Load failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PopulateGrid(IniDocument doc)
        {
            _grid.Rows.Clear();
            foreach (var pair in doc.Pairs) _grid.Rows.Add(pair.Section, pair.Key, pair.Value);
        }

        private IEnumerable<IniPair> EnumerateGrid()
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow) continue;
                var sec = row.Cells[0].Value?.ToString();
                var key = row.Cells[1].Value?.ToString();
                var val = row.Cells[2].Value?.ToString();
                if (string.IsNullOrWhiteSpace(sec) || string.IsNullOrWhiteSpace(key)) continue;
                yield return new IniPair(sec.Trim(), key.Trim(), val ?? string.Empty);
            }
        }

        private void SaveIni()
        {
            try
            {
                if (string.IsNullOrEmpty(_loadedPath))
                {
                    var guess = _pathBox.Text.Trim();
                    if (string.IsNullOrWhiteSpace(guess)) { MessageBox.Show("No file path specified.", "Save", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                    _loadedPath = guess;
                }

                var dir = Path.GetDirectoryName(_loadedPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                // backup
                var backupPath = _loadedPath + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                if (File.Exists(_loadedPath)) File.Copy(_loadedPath, backupPath, overwrite: false);

                // write atomically
                var temp = _loadedPath + ".tmp";
                var pairs = EnumerateGrid().ToList();
                var content = IniDocument.Serialize(pairs);
                File.WriteAllText(temp, content, new UTF8Encoding(false));
                if (File.Exists(_loadedPath)) File.Delete(_loadedPath);
                File.Move(temp, _loadedPath);

                _statusLabel.Text = $"Saved {pairs.Count} entries. Backup: {Path.GetFileName(backupPath)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Save failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Backup()
        {
            try
            {
                if (string.IsNullOrEmpty(_loadedPath) || !File.Exists(_loadedPath)) { MessageBox.Show("Load a file first.", "Backup", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                var backupPath = _loadedPath + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                File.Copy(_loadedPath, backupPath, overwrite: false);
                _statusLabel.Text = "Backup created: " + Path.GetFileName(backupPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Backup failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void EnsureRow(string section, string key, string value)
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow) continue;
                var sec = row.Cells[0].Value?.ToString();
                var k = row.Cells[1].Value?.ToString();
                if (string.Equals(sec, section, StringComparison.OrdinalIgnoreCase) && string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                { row.Cells[2].Value = value; return; }
            }
            _grid.Rows.Add(section, key, value);
        }

        private void ApplyFilter()
        {
            var q = (_searchBox.Text ?? string.Empty).Trim();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow) continue;
                if (string.IsNullOrEmpty(q)) { row.Visible = true; continue; }
                var s = (row.Cells[0].Value?.ToString() ?? string.Empty);
                var k = (row.Cells[1].Value?.ToString() ?? string.Empty);
                var v = (row.Cells[2].Value?.ToString() ?? string.Empty);
                row.Visible = s.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                              k.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                              v.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
    }

    public sealed class AboutForm : Form
    {
        public AboutForm()
        {
            Text = "About";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false; MinimizeBox = false;
            Width = 440; Height = 240;

            var asm = Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(asm.Location);
            var buildTime = File.GetLastWriteTime(asm.Location);

            var lbl = new Label { Dock = DockStyle.Fill, Padding = new Padding(16), AutoSize = false };
            lbl.Text = $"{fvi.ProductName} {fvi.FileVersion}\n© {DateTime.Now:yyyy} {fvi.CompanyName}\n\n{fvi.Comments}\nBuild date: {buildTime:yyyy-MM-dd HH:mm}\nPath: {asm.Location}";

            var btn = new Button { Text = "OK", DialogResult = DialogResult.OK, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            btn.SetBounds(Width - 120, Height - 90, 80, 30);

            Controls.Add(lbl);
            Controls.Add(btn);
            AcceptButton = btn;
        }
    }

    public sealed class IniPair
    {
        public string Section { get; }
        public string Key { get; }
        public string Value { get; }
        public IniPair(string section, string key, string value) { Section = section; Key = key; Value = value; }
    }

    public sealed class IniDocument
    {
        public List<IniPair> Pairs { get; } = new List<IniPair>();
        public int Count => Pairs.Count;

        public static IniDocument LoadFromFile(string path) => Parse(File.ReadAllText(path));

        public static IniDocument Parse(string text)
        {
            var doc = new IniDocument();
            string currentSection = string.Empty;
            using (var reader = new StringReader(text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith(";") || trimmed.StartsWith("#") || trimmed.Length == 0) continue; // comments/blank
                    var secMatch = Regex.Match(trimmed, @"^\[(.+)\]$");
                    if (secMatch.Success) { currentSection = secMatch.Groups[1].Value.Trim(); continue; }
                    var eq = trimmed.IndexOf('=');
                    if (eq > 0)
                    {
                        var key = trimmed.Substring(0, eq).Trim();
                        var val = trimmed.Substring(eq + 1).Trim();
                        doc.Pairs.Add(new IniPair(currentSection, key, val));
                    }
                }
            }
            return doc;
        }

        public static string Serialize(IEnumerable<IniPair> pairs)
        {
            var sb = new StringBuilder();
            foreach (var group in pairs.GroupBy(p => p.Section ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                var section = group.Key;
                if (!string.IsNullOrEmpty(section)) sb.Append('[').Append(section).AppendLine("]");
                foreach (var p in group) sb.Append(p.Key).Append('=').AppendLine(p.Value ?? string.Empty);
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }

    public static class PresetJson
    {
        // Minimal JSON (de)serializer to avoid external deps.
        // Format: [{"section":"...","key":"...","value":"..."}, ...]
        public static List<IniPair> Parse(string json)
        {
            var list = new List<IniPair>();
            try
            {
                var rxItem = new Regex("\\{([^}]+)\\}", RegexOptions.Multiline);
                var rxKV = new Regex("\"(section|key|value)\"\\s*:\\s*\"(.*?)\"", RegexOptions.IgnoreCase);
                foreach (Match m in rxItem.Matches(json))
                {
                    string sec = string.Empty, key = string.Empty, val = string.Empty;
                    foreach (Match kv in rxKV.Matches(m.Value))
                    {
                        var name = kv.Groups[1].Value.ToLowerInvariant();
                        var value = Unescape(kv.Groups[2].Value);
                        if (name == "section") sec = value; else if (name == "key") key = value; else if (name == "value") val = value;
                    }
                    if (!string.IsNullOrWhiteSpace(key)) list.Add(new IniPair(sec, key, val));
                }
            }
            catch { }
            return list;
        }

        public static string Serialize(IEnumerable<IniPair> items)
        {
            var sb = new StringBuilder();
            sb.Append('[');
            bool first = true;
            foreach (var it in items)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"section\":\"").Append(Escape(it.Section)).Append("\",")
                  .Append("\"key\":\"").Append(Escape(it.Key)).Append("\",")
                  .Append("\"value\":\"").Append(Escape(it.Value)).Append("\"}");
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static string Escape(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        private static string Unescape(string s) => (s ?? string.Empty).Replace("\\\"", "\"").Replace("\\\\", "\\");
    }
}

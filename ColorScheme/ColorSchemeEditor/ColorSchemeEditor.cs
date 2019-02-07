using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using DrawingEx.ColorManagement;
using DrawingEx.ColorManagement.ColorModels;

namespace ColorScheme
{
    public class ColorSchemeEditor
    {
        public ColorSchemeEditor(ColorSchemeController controller, string saveDirectory, Control container)
        {
            _container = container;

            _container.Controls.Add(_layoutPanel);

            _cellSize = new Size(96, 36);
            _cellMargin = new Padding(3);

            foreach (Control control in _layoutPanel.Controls)
            {
                control.Size = _cellSize;
                control.Margin = _cellMargin;
            }

            updateSize();

            _controller = controller;
            _saveDirectory = saveDirectory;

            foreach (KnownColor colorName in _controller.KnownColors)
                _layoutPanel.Controls.Add(createColorCell(colorName));

            addButtons();

            updateSize();

            _colorPicker = createColorPicker();

            void HandleContainerOnDisposed(object s, EventArgs e)
            {
                _colorPicker.Hide();
                _colorPicker.Dispose();

                _container.Disposed -= HandleContainerOnDisposed;
            }

            _container.Disposed += HandleContainerOnDisposed;
        }

        private void updateSize()
        {
            int columnsCount = (int)Math.Ceiling(Math.Sqrt(_layoutPanel.Controls.Count));
            int rowsCount = columnsCount != 0
                ? (int)Math.Ceiling((float)_layoutPanel.Controls.Count / columnsCount)
                : 0;

            var cellCount = new Size(columnsCount, rowsCount);

            _container.ClientSize = _container.ClientSize - _container.DisplayRectangle.Size
                + _layoutPanel.Margin.Size
                + _container.Padding.Size
                + new Size(
                    _cellMargin.Size.Width * cellCount.Width,
                    _cellMargin.Size.Height * cellCount.Height)
                + new Size(
                    _cellSize.Width * cellCount.Width,
                    _cellSize.Height * cellCount.Height);
        }

        private Control createColorCell(KnownColor color)
        {
            var control = new Label
            {
                AutoSize = false,
                Size = _cellSize,
                Margin = _cellMargin,
                BackColor = Color.FromKnownColor(color),
                TextAlign = ContentAlignment.MiddleCenter,
                BorderStyle = BorderStyle.None
            };

            control.MouseEnter += (s, e) => { selectForeColor(control, hover: true); };
            control.MouseLeave += (s, e) => { selectForeColor(control, hover: false); };

            updateText();
            ColorSchemeController.SystemColorsChanging += updateText;

            void updateText()
            {
                var result = new StringBuilder();
                result.Append(color);

                if (_controller.GetOriginalColor(color) != _controller.GetColor(color))
                {
                    result.AppendLine("*");
                    result.Append("middle-click to reset");
                }

                control.Text = result.ToString();
            }

            bindForeColor(control);

            control.SetTag(color);

            control.MouseUp += (t, te) =>
            {
                switch (te.Button)
                {
                    case MouseButtons.Left:
                        _colorPicker.DialogResult = DialogResult.None;
                        _colorPicker.Color = XYZ.FromRGB(new RGB(control.BackColor));
                        _colorPicker.Text = color.ToString();
                        _colorPicker.SetTag(color);
                        _colorPicker.Location = _container.PointToScreen(new Point(_container.ClientSize.Width, 0));

                        if (!_colorPicker.Visible)
                            _colorPicker.Show();

                        if (!_colorPicker.Focused)
                            _colorPicker.Focus();

                        break;

                    case MouseButtons.Middle:
                        _controller.Reset(color);
                        saveCurrentColorScheme();
                        break;
                }
            };

            return control;
        }

        private ColorPicker createColorPicker()
        {
            var picker = new ColorPicker
            {
                StartPosition = FormStartPosition.Manual,
                TopMost = true
            };

            picker.FormClosing += closing;

            void closing(object s, FormClosingEventArgs e)
            {
                if (picker.DialogResult != DialogResult.OK)
                    return;

                var knownColor = picker.GetTag<KnownColor>();
                var backColor = picker.Color.ToRGB().ToArgb();
                _controller.SetColor(knownColor, backColor.ToArgb());
                saveCurrentColorScheme();
            }

            return picker;
        }

        private void addButtons()
        {
            bool resetting = false;
            createButton("Reset", (s, e) =>
            {
                if (resetting)
                    return;

                resetting = true;
                _controller.ResetAll();
                saveCurrentColorScheme();
                resetting = false;
            });

            createButton("Save", (s, e) =>
            {
                if (saveDialog())
                    saveCurrentColorScheme();
            });

            createButton("Load", (s, e) =>
            {
                if (trySelectFile(out string selectedFile) && load(selectedFile))
                    saveCurrentColorScheme();

                bool trySelectFile(out string result)
                {
                    result = null;

                    var dlg = new OpenFileDialog
                    {
                        DefaultExt = Ext,
                        InitialDirectory = _saveDirectory,
                        AddExtension = true,
                        Filter = _filter,
                        Title = "Load color scheme",
                        CheckFileExists = true
                    };

                    if (dlg.ShowDialog() != DialogResult.OK)
                        return false;

                    result = dlg.FileName;
                    return true;
                }
            });

            bool saveDialog()
            {
                var dlg = new SaveFileDialog
                {
                    DefaultExt = Ext,
                    InitialDirectory = _saveDirectory,
                    AddExtension = true,
                    Filter = _filter,
                    Title = "Save color scheme",
                    CheckPathExists = true
                };

                if (dlg.ShowDialog() != DialogResult.OK)
                    return false;

                return save(_controller.Save(), dlg.FileName);
            }
        }

        private void createButton(string text, EventHandler clickHandler)
        {
            var control = new CheckBox
            {
                AutoSize = false,
                AutoCheck = false,
                Appearance = Appearance.Button,
                Size = _cellSize,
                Margin = _cellMargin,
                BackColor = SystemColors.Control,
                ForeColor = SystemColors.ControlText,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = text
            };

            bindForeColor(control);
            control.Click += clickHandler;
            _layoutPanel.Controls.Add(control);
        }

        private static void bindForeColor(Control c)
        {
            selectForeColor(c);
            ColorSchemeController.SystemColorsChanging += () => selectForeColor(c);
        }

        private static void selectForeColor(Control c, bool hover = false)
        {
            float invert(float _) =>
                (1f - (float)Math.Round(_)).WithinRange(0.3f, 0.7f);

            float shift(float _) =>
                (_ + 0.4f * Math.Sign(_ - 0.5f)).Modulo(1);

            if (hover)
                c.ForeColor = c.BackColor.TransformHsv(v: invert).TransformHsv(h: _ => _ + 60f, s: invert, v: shift);
            else
                c.ForeColor = c.BackColor.TransformHsv(v: invert);

            c.Invalidate();
        }

        private void saveCurrentColorScheme()
        {
            string file = currentColorsFile();
            var values = _controller.Save();

            if (values.Count == 0)
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            else
                save(values, file, quiet: true);
        }

        public void LoadCurrentColorScheme() =>
            load(currentColorsFile(), quiet: true);

        private static bool save(IReadOnlyDictionary<int, int> values, string file, bool quiet = false)
        {
            string serialized = string.Join(Environment.NewLine, values.Select(_ => string.Format(Format, (KnownColor)_.Key, _.Value)));
            try
            {
                File.WriteAllText(file, serialized);
                return true;
            }
            catch (Exception ex)
            {
                if (!quiet)
                    MessageBox.Show("Failed to write file: " + file + Environment.NewLine + ex.Message);

                return false;
            }
        }

        private bool load(string file, bool quiet = false)
        {
            if (tryReadFile(file, out string serialized) && tryDeserialize(serialized, out var deserialized))
            {
                _controller.Load(deserialized);
                return true;
            }

            return false;

            bool tryReadFile(string fileName, out string result)
            {
                result = null;
                var fileInfo = new FileInfo(fileName);

                if (!fileInfo.Exists)
                {
                    warnOnInvalidContent("File not found");
                    return false;
                }

                if (fileInfo.Length > 1024 * 1024)
                {
                    warnOnInvalidContent("File too large");
                    return false;
                }

                try
                {
                    result = File.ReadAllText(fileName);
                    return true;
                }
                catch (Exception ex)
                {
                    warnOnInvalidContent(ex.Message);
                    return false;
                }
            }

            bool tryDeserialize(string input, out IReadOnlyDictionary<int, int> result)
            {
                result = null;
                var lines = input.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                var map = new Dictionary<int, int>();

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    var match = _pattern.Match(line);
                    if (!match.Success)
                    {
                        warnOnInvalidContent($"Invalid format of line {i + 1}: {line}");
                        return false;
                    }

                    string name = match.Groups["name"].Value;
                    string rgbaStr = match.Groups["argb"].Value;

                    if (!Enum.TryParse(name, out KnownColor colorName))
                    {
                        warnOnInvalidContent($"Invalid color name at line {i + 1}: {name}");
                        return false;
                    }

                    if (!_controller.KnownColors.Contains(colorName))
                        continue;

                    if (!int.TryParse(rgbaStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgba))
                    {
                        warnOnInvalidContent($"Invalid color value at line {i + 1}: {rgbaStr}");
                        return false;
                    }

                    map.Add((int)colorName, rgba);
                }

                result = map;
                return true;
            }

            void warnOnInvalidContent(string message)
            {
                if (!quiet)
                    MessageBox.Show("Failed to read color scheme from file: " + file + Environment.NewLine + message);
            }
        }

        private string currentColorsFile() =>
            Path.Combine(_saveDirectory, CurrentSchemeName + Ext);

        public IEnumerable<string> GetSavedSchemeNames() =>
            Directory
                .GetFiles(_saveDirectory, "*" + Ext, SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => !String.Equals(n, CurrentSchemeName, StringComparison.InvariantCultureIgnoreCase))
                .OrderByDescending(n => String.Equals(n, "system default", StringComparison.InvariantCultureIgnoreCase));

        public void LoadSavedScheme(string name)
        {
            if (load(Path.Combine(_saveDirectory, name + Ext)))
                saveCurrentColorScheme();
        }

        private readonly FlowLayoutPanel _layoutPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };

        private readonly string _saveDirectory;

        private const string CurrentSchemeName = "current";
        private const string Ext = ".colors";
        private const string Format = "{0}: {1:x8}";
        private static readonly string _filter = $"Git Extensions color scheme (*{Ext})|*{Ext}";
        private static readonly Regex _pattern = new Regex(@"^(?<name>\w+): (?<argb>[\da-f]{8})$", RegexOptions.IgnoreCase);

        private readonly Size _cellSize;
        private readonly Padding _cellMargin;
        private readonly ColorSchemeController _controller;
        private readonly ColorPicker _colorPicker;
        private readonly Control _container;
    }
}
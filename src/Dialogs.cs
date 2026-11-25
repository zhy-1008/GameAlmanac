using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace GameAlmanac
{
    internal static class Dialogs
    {
        public static string EditGame(Window owner, LibraryStore store, Library library, Game game, int? startYear)
        {
            var name = Ui.Box(game == null ? "" : game.Name, "游戏名称");
            var type = TypePicker(library, game == null ? library.Types[0].Id : game.TypeId);
            var year = Ui.Box((startYear ?? DateTime.Today.Year).ToString(CultureInfo.InvariantCulture), "游玩年份", 4);
            var note = Ui.Box(game == null ? "" : game.Note, "我的评语", 4000);
            note.AcceptsReturn = true;
            note.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            note.TextWrapping = TextWrapping.Wrap;
            note.MinHeight = 80;

            string id = null;
            string source = null;
            FormWindow dialog = null;
            dialog = new FormWindow(owner, game == null ? "添加游戏" : "编辑游戏", "保存游戏", () =>
            {
                var chosenType = (GameType)type.SelectedItem;
                int? firstYear = game == null ? (int?)ReadYear(year.Text) : null;
                id = store.SaveGame(game == null ? null : game.Id, name.Text, chosenType.Id, note.Text.Trim(), firstYear, source);
                return true;
            });

            var coverGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            coverGrid.ColumnDefinitions.Add(Ui.Pixels(64));
            coverGrid.ColumnDefinitions.Add(Ui.Star());
            var preview = new Border { Width = 60, Height = 80, HorizontalAlignment = HorizontalAlignment.Left };
            preview.Child = Ui.Cover(game == null ? null : store.LoadCover(game.Cover), "封面");
            Ui.Put(coverGrid, preview, 0);
            var coverOptions = new StackPanel { Margin = new Thickness(11, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            coverOptions.Children.Add(Ui.Text("游戏封面", 13, Ui.Ink, true));
            var filename = Ui.Text(game == null ? "选择一张本地图片。" : "可更换现有封面。", 12, Ui.Muted);
            var browse = Ui.Button("选择本地图片", () =>
            {
                var picker = new OpenFileDialog {
                    Title = "选择游戏封面", CheckFileExists = true, Multiselect = false,
                    Filter = "图片 (*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.ico)|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.ico|所有文件 (*.*)|*.*"
                };
                if (picker.ShowDialog(dialog) != true) return;
                try
                {
                    BitmapSource image = LibraryStore.ReadImage(picker.FileName);
                    source = picker.FileName;
                    preview.Child = Ui.Cover(image, "封面");
                    filename.Text = Path.GetFileName(source);
                    dialog.Fail("");
                }
                catch (Exception)
                {
                    dialog.Fail("无法读取这张图片，请选择另一张封面。");
                }
            });
            browse.HorizontalAlignment = HorizontalAlignment.Left;
            browse.Margin = new Thickness(0, 5, 0, 4);
            coverOptions.Children.Add(browse);
            coverOptions.Children.Add(filename);
            Ui.Put(coverGrid, coverOptions, 1);
            dialog.Body.Children.Add(coverGrid);
            dialog.Body.Children.Add(Ui.Field("游戏名称", name));
            var columns = new Grid();
            columns.ColumnDefinitions.Add(Ui.Star());
            if (game == null) columns.ColumnDefinitions.Add(Ui.Star());
            var typeField = Ui.Field("游戏类型", type);
            var templateLabel = Ui.Text("", 12, Ui.Muted);
            templateLabel.Margin = new Thickness(0, 4, 0, 0);
            Action refreshTemplate = () => templateLabel.Text = "使用「" + library.Template(((GameType)type.SelectedItem).TemplateId).Name + "」评分模板";
            type.SelectionChanged += (sender, args) => refreshTemplate();
            refreshTemplate();
            typeField.Children.Add(templateLabel);
            Ui.Put(columns, typeField, 0);
            if (game == null)
            {
                typeField.Margin = new Thickness(0, 0, 11, 11);
                Ui.Put(columns, Ui.Field("游玩年份", year), 1);
            }
            dialog.Body.Children.Add(columns);
            dialog.Body.Children.Add(Ui.Field("我的评语（选填）", note));
            dialog.Loaded += (sender, args) => name.Focus();
            return dialog.ShowDialog() == true ? id : null;
        }

        public static int? AddPlay(Window owner, LibraryStore store, Library library, Game game)
        {
            int currentYear = DateTime.Today.Year;
            var year = Ui.Box(game.FindPlay(currentYear) == null ? currentYear.ToString(CultureInfo.InvariantCulture) : "", "游玩年份", 4);
            var independent = new RadioButton { Content = "独立评分", IsChecked = true, GroupName = "NewPlayMode", Padding = new Thickness(4, 2, 0, 2) };
            var reuse = new RadioButton { Content = "沿用上次评分", GroupName = "NewPlayMode", Padding = new Thickness(4, 2, 0, 2) };
            var previousText = Ui.Text("", 12, Ui.Muted);
            int selectedYear = 0;
            var dialog = new FormWindow(owner, "添加游玩年份", "继续评分", () =>
            {
                selectedYear = ReadYear(year.Text);
                if (game.FindPlay(selectedYear) != null) throw new InvalidOperationException("这一年已有游玩记录。");
                if (reuse.IsChecked == true) store.AddReusedPlay(game.Id, selectedYear);
                return true;
            });
            dialog.Body.Children.Add(Ui.Field("游玩年份", year));
            dialog.Body.Children.Add(Ui.Text("本次评分", 13, Ui.Ink, true));
            var choices = new Grid { Margin = new Thickness(0, 7, 0, 9) };
            choices.ColumnDefinitions.Add(Ui.Star());
            choices.ColumnDefinitions.Add(Ui.Star());
            var directChoice = new StackPanel();
            directChoice.Children.Add(independent);
            directChoice.Children.Add(Ui.Text("为这个年份填写一份新评分。", 12, Ui.Muted));
            var reuseChoice = new StackPanel();
            reuseChoice.Children.Add(reuse);
            reuseChoice.Children.Add(previousText);
            var directCard = Ui.Card(directChoice, Ui.Base, 9);
            directCard.Margin = new Thickness(0, 0, 7, 0);
            Ui.Put(choices, directCard, 0);
            Ui.Put(choices, Ui.Card(reuseChoice, Ui.Base, 9), 1);
            dialog.Body.Children.Add(choices);

            Action update = () =>
            {
                int value;
                bool valid = int.TryParse(year.Text, out value) && value >= 1970 && value <= currentYear;
                var previous = valid ? game.PreviousPlay(value) : null;
                if (previous == null) independent.IsChecked = true;
                reuse.IsEnabled = previous != null;
                previousText.Text = previous != null
                    ? "使用 " + previous.Year + " 年评分，随原评分更新。"
                    : valid ? "这个年份之前没有游玩记录。" : "填写年份后查看可沿用的评分。";
                dialog.Save.Content = reuse.IsChecked == true ? "保存游玩记录" : "继续评分";
            };
            year.TextChanged += (sender, args) => update();
            independent.Checked += (sender, args) => dialog.Save.Content = "继续评分";
            reuse.Checked += (sender, args) => dialog.Save.Content = "保存游玩记录";
            dialog.Loaded += (sender, args) => { year.Focus(); year.SelectAll(); };
            update();
            if (dialog.ShowDialog() != true) return null;
            if (reuse.IsChecked == true) return selectedYear;
            return new ScoreWindow(owner, store, game, library.TemplateFor(game), new Play(selectedYear), true).ShowDialog() == true
                ? (int?)selectedYear : null;
        }

        public static ComboBox TemplatePicker(Library library, string selected)
        {
            return new ComboBox {
                ItemsSource = library.Templates, DisplayMemberPath = "Name", SelectedValuePath = "Id", SelectedValue = selected
            };
        }

        public static bool DeleteGame(Window owner, LibraryStore store, Game game)
        {
            return ConfirmDelete(owner, "删除游戏",
                "删除「" + game.Name + "」及其 " + game.Plays.Count + " 条游玩记录、评分、评语和封面？",
                () => store.DeleteGame(game.Id));
        }

        public static bool ConfirmDelete(Window owner, string title, string message, Action delete,
            ComboBox replacement = null, string replacementLabel = null)
        {
            var dialog = new FormWindow(owner, title, "确认删除", () => { delete(); return true; }, 480, true);
            var explanation = Ui.Text(message, 14, Ui.Ink);
            explanation.LineHeight = 23;
            explanation.Margin = new Thickness(0, 0, 0, 9);
            dialog.Body.Children.Add(explanation);
            if (replacement != null)
            {
                dialog.Body.Children.Add(Ui.Field(replacementLabel, replacement));
                dialog.Save.IsEnabled = replacement.SelectedItem != null;
                replacement.SelectionChanged += (sender, args) => dialog.Save.IsEnabled = replacement.SelectedItem != null;
            }
            return dialog.ShowDialog() == true;
        }

        private static ComboBox TypePicker(Library library, string selected)
        {
            return new ComboBox {
                ItemsSource = library.Types, DisplayMemberPath = "Name", SelectedValuePath = "Id", SelectedValue = selected
            };
        }

        private static int ReadYear(string text)
        {
            int year;
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out year) || year < 1970 || year > DateTime.Today.Year)
                throw new InvalidOperationException("请输入 1970 至 " + DateTime.Today.Year + " 之间的游玩年份。");
            return year;
        }
    }

    internal sealed class WeightFields
    {
        private readonly TextBox[] inputs = new TextBox[Rating.Metrics.Length];
        private readonly Border[] bars = new Border[Rating.Metrics.Length];
        private readonly TextBlock total = Ui.Text("", 13, Ui.Green, true);
        public readonly StackPanel Content = new StackPanel();

        public WeightFields(int[] weights)
        {
            for (int index = 0; index < inputs.Length; index++)
            {
                var row = new Grid { MinHeight = 41 };
                row.ColumnDefinitions.Add(Ui.Pixels(95));
                row.ColumnDefinitions.Add(Ui.Star());
                row.ColumnDefinitions.Add(Ui.Pixels(71));
                var label = Ui.Text(Rating.Metrics[index].Name, 13, Ui.Ink, true);
                Ui.Put(row, label, 0);
                bars[index] = new Border { VerticalAlignment = VerticalAlignment.Center };
                Ui.Put(row, bars[index], 1);
                var field = Ui.Box(weights[index].ToString(CultureInfo.InvariantCulture), Rating.Metrics[index].Name + "权重", 3);
                field.Width = 49;
                field.TextAlignment = TextAlignment.Right;
                var percent = Ui.Text(" %", 12, Ui.Muted);
                Ui.Put(row, Ui.LineUp(field, percent), 2);
                inputs[index] = field;
                field.TextChanged += (sender, args) => Refresh();
                Content.Children.Add(new Border { BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 1, 0, 0), Child = row });
            }
            total.Margin = new Thickness(0, 8, 0, 6);
            Content.Children.Add(total);
            Refresh();
        }

        private void Refresh()
        {
            var weights = Read();
            for (int index = 0; index < inputs.Length; index++)
            {
                int weight;
                int.TryParse(inputs[index].Text, out weight);
                bars[index].Child = Ui.Bar(weight, 100);
            }
            total.Text = weights == null ? "每项权重须为 0–100 的整数。" : "合计 " + weights.Sum() + "%";
            total.Foreground = weights != null && weights.Sum() == 100 ? Ui.Green : Ui.Brush("Error");
        }

        private int[] Read()
        {
            var weights = new int[inputs.Length];
            for (int index = 0; index < inputs.Length; index++)
                if (!int.TryParse(inputs[index].Text, NumberStyles.None, CultureInfo.InvariantCulture, out weights[index])
                    || weights[index] > 100) return null;
            return weights;
        }

        public int[] ReadValid()
        {
            var weights = Read();
            if (weights == null || weights.Sum() != 100) throw new InvalidOperationException("请将六项权重的合计调整为 100%。");
            return weights;
        }
    }

    internal sealed class ScoreWindow
    {
        private readonly Play draft;
        private readonly WeightTemplate template;
        private readonly FormWindow window;
        private readonly RadioButton direct = new RadioButton { Content = "一级评分（6 项）", GroupName = "RatingMode" };
        private readonly RadioButton detailed = new RadioButton { Content = "二级评分（18 项）", GroupName = "RatingMode" };
        private readonly StackPanel fields = new StackPanel();
        private readonly TextBlock preview = Ui.Text("", 17, Ui.Green, true);
        private readonly List<ScoreField> values = new List<ScoreField>();
        private readonly Dictionary<int, TextBlock> groupLabels = new Dictionary<int, TextBlock>();
        private bool changingMode;

        public ScoreWindow(Window owner, LibraryStore store, Game game, WeightTemplate template, Play scoring, bool createYear)
        {
            this.template = template;
            draft = scoring.IndependentCopy(scoring.Year);
            window = new FormWindow(owner, scoring.Year + " 年评分", "保存评分", () =>
            {
                var current = draft.IndependentCopy(draft.Year);
                if (!ReadFields(current, true)) return false;
                store.SaveScore(game.Id, current, createYear);
                return true;
            }, 630);
            window.Body.Children.Add(Ui.Text(game.Name + "　·　" + template.Name, 13, Ui.Muted));
            var choices = Ui.LineUp(direct, detailed);
            choices.Margin = new Thickness(0, 13, 0, 8);
            detailed.Margin = new Thickness(20, 0, 0, 0);
            window.Body.Children.Add(choices);
            var guide = Ui.Text("拖动滑条或输入 0–10 分，支持一位小数。清空数字框可恢复未评分。", 12, Ui.Muted);
            guide.Margin = new Thickness(0, 0, 0, 10);
            window.Body.Children.Add(guide);
            window.Body.Children.Add(Ui.Card(fields, Ui.Paper, 9));
            preview.Margin = new Thickness(0, 11, 0, 4);
            window.Body.Children.Add(preview);
            direct.IsChecked = !draft.Detailed;
            detailed.IsChecked = draft.Detailed;
            direct.Checked += (sender, args) => SwitchMode(false);
            detailed.Checked += (sender, args) => SwitchMode(true);
            BuildFields();
        }

        public bool? ShowDialog() { return window.ShowDialog(); }

        private void SwitchMode(bool useDetails)
        {
            if (changingMode || draft.Detailed == useDetails) return;
            if (!ReadFields(draft, true))
            {
                changingMode = true;
                (draft.Detailed ? detailed : direct).IsChecked = true;
                changingMode = false;
                return;
            }
            draft.Detailed = useDetails;
            window.Fail("");
            BuildFields();
        }

        private void BuildFields()
        {
            fields.Children.Clear();
            values.Clear();
            groupLabels.Clear();
            for (int metric = 0; metric < Rating.Metrics.Length; metric++)
            {
                if (!draft.Detailed)
                {
                    fields.Children.Add(AddField(Rating.Metrics[metric].Name, metric, -1, null));
                    continue;
                }
                var expander = new Expander { IsExpanded = metric == 0, Foreground = Ui.Ink, Padding = new Thickness(0, 3, 0, 4) };
                var header = Ui.Text(Rating.Metrics[metric].Name, 13, Ui.Ink, true);
                var output = Ui.Text("", 12, Ui.Green, true);
                output.Margin = new Thickness(11, 0, 0, 0);
                expander.Header = Ui.LineUp(header, output);
                groupLabels.Add(metric, output);
                var body = new StackPanel { Margin = new Thickness(15, 0, 0, 0) };
                for (int item = 0; item < Rating.Metrics[metric].Items.Length; item++)
                    body.Children.Add(AddField(Rating.Metrics[metric].Items[item], metric, item, expander));
                expander.Content = body;
                fields.Children.Add(new Border { BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 0, 1), Child = expander });
            }
            Refresh();
        }

        private FrameworkElement AddField(string caption, int metric, int item, Expander group)
        {
            double? score = item < 0 ? draft.Direct[metric] : draft.Details[metric][item];
            bool notApplicable = Rating.IsNotApplicable(score);
            string text = !score.HasValue || notApplicable ? "" : score.Value.ToString("0.#", CultureInfo.InvariantCulture);
            var input = Ui.Box(text, caption + "分数", 4);
            input.IsEnabled = !notApplicable;
            input.TextAlignment = TextAlignment.Right;
            input.VerticalAlignment = VerticalAlignment.Center;
            var slider = new Slider
            {
                Minimum = 0, Maximum = 10, SmallChange = 0.1, LargeChange = 1, TickFrequency = 0.1,
                IsSnapToTickEnabled = true, IsMoveToPointEnabled = true, IsEnabled = !notApplicable,
                Value = text.Length == 0 ? 0 : score.Value, Tag = text.Length == 0 ? "Unscored" : null,
                Margin = new Thickness(0, 0, 13, 0)
            };
            slider.SetResourceReference(FrameworkElement.StyleProperty, "ScoreSlider");
            AutomationProperties.SetName(slider, caption + "评分滑条");
            var skip = new CheckBox { Content = "不适用", IsChecked = notApplicable, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(9, 0, 0, 0) };
            AutomationProperties.SetName(skip, caption + "不适用");
            var emptyHint = Ui.Text("未评分", 11, Ui.Muted);
            emptyHint.TextAlignment = TextAlignment.Right;
            emptyHint.Margin = new Thickness(4, 0, 8, 0);
            emptyHint.IsHitTestVisible = false;
            var number = new Grid();
            number.Children.Add(input);
            number.Children.Add(emptyHint);
            var label = Ui.Text(caption, 13, Ui.Ink, true);
            var row = new Grid { MinHeight = 43 };
            row.ColumnDefinitions.Add(Ui.Pixels(111));
            row.ColumnDefinitions.Add(Ui.Star());
            row.ColumnDefinitions.Add(Ui.Pixels(70));
            row.ColumnDefinitions.Add(Ui.Pixels(83));
            Ui.Put(row, label, 0);
            Ui.Put(row, slider, 1);
            Ui.Put(row, number, 2);
            Ui.Put(row, skip, 3);
            values.Add(new ScoreField { Metric = metric, Item = item, Name = caption, Input = input, Skip = skip, Group = group });

            bool synchronizing = false;
            Action updateHint = () => emptyHint.Visibility = input.Text.Trim().Length == 0 && skip.IsChecked != true
                ? Visibility.Visible : Visibility.Collapsed;
            updateHint();
            Action applySlider = () =>
            {
                if (synchronizing || !slider.IsEnabled) return;
                synchronizing = true;
                input.Text = slider.Value.ToString("0.#", CultureInfo.InvariantCulture);
                slider.Tag = null;
                synchronizing = false;
                updateHint();
                Refresh();
            };
            slider.ValueChanged += (sender, args) => applySlider();
            // Selecting the existing thumb position is intentional even when ValueChanged does not fire.
            slider.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler((sender, args) =>
            {
                if (slider.Tag != null && slider.IsEnabled) applySlider();
            }), true);
            slider.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler((sender, args) =>
            {
                switch (args.Key)
                {
                    case Key.Left: case Key.Right: case Key.Up: case Key.Down:
                    case Key.Home: case Key.End: case Key.PageUp: case Key.PageDown:
                        if (slider.Tag != null && slider.IsEnabled) applySlider();
                        break;
                }
            }), true);
            input.TextChanged += (sender, args) =>
            {
                if (synchronizing) return;
                double? typed;
                bool valid = Rating.TryScore(input.Text, out typed) && typed.HasValue;
                synchronizing = true;
                slider.Value = valid ? typed.Value : 0;
                slider.Tag = valid ? null : "Unscored";
                synchronizing = false;
                updateHint();
                Refresh();
            };
            RoutedEventHandler updateSkip = (sender, args) =>
            {
                input.IsEnabled = slider.IsEnabled = skip.IsChecked != true;
                updateHint();
                Refresh();
            };
            skip.Checked += updateSkip;
            skip.Unchecked += updateSkip;
            return new Border { BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 0, 1), Child = row };
        }

        private bool ReadFields(Play target, bool showError)
        {
            foreach (var field in values)
            {
                double? score;
                if (field.Skip.IsChecked == true) score = double.NaN;
                else if (!Rating.TryScore(field.Input.Text, out score))
                {
                    if (showError)
                    {
                        window.Fail(field.Name + "需为 0–10 分，最多一位小数。");
                        if (field.Group != null) field.Group.IsExpanded = true;
                        field.Input.Focus();
                    }
                    return false;
                }
                if (field.Item < 0) target.Direct[field.Metric] = score;
                else target.Details[field.Metric][field.Item] = score;
            }
            return true;
        }

        private void Refresh()
        {
            var temporary = draft.IndependentCopy(draft.Year);
            if (!ReadFields(temporary, false))
            {
                preview.Text = "总分预览  ·  请检查分数";
                return;
            }
            var result = Rating.Calculate(temporary, template.Weights);
            preview.Text = "总分预览  ·  " + Rating.Format(result.Total);
            foreach (var entry in groupLabels) entry.Value.Text = Rating.Format(result.Scores[entry.Key]);
        }

        private sealed class ScoreField
        {
            public int Metric;
            public int Item;
            public string Name;
            public TextBox Input;
            public CheckBox Skip;
            public Expander Group;
        }
    }
}

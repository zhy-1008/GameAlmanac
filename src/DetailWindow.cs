using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace GameAlmanac
{
    internal sealed class DetailWindow : Window
    {
        private readonly LibraryStore store;
        private readonly string id;
        private readonly Func<Library> refresh;
        private readonly WindowFrame frame;
        private Library library;
        private int year;
        public string OpenTemplateId { get; private set; }

        public DetailWindow(Window owner, LibraryStore store, Library library, string gameId, int year, Func<Library> refresh)
        {
            this.store = store;
            this.library = library;
            id = gameId;
            this.year = year;
            this.refresh = refresh;
            Ui.SetWindow(this, "评分详情");
            Background = Ui.Paper;
            Owner = owner;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            Width = 690;
            SizeToContent = SizeToContent.Height;
            MaxHeight = Math.Max(410, SystemParameters.WorkArea.Height - 72);
            frame = new WindowFrame(this, false, "评分详情");
            Content = frame;
            PreviewKeyDown += (sender, args) =>
            {
                if (args.Key != Key.Escape) return;
                args.Handled = true;
                Close();
            };
            Render();
        }

        private void SelectYear(int value)
        {
            year = value;
            Render();
        }

        private void Render()
        {
            var game = library.Game(id);
            var play = game.FindPlay(year);
            var original = game.ScoreSource(play);
            var score = original ?? new Play(play.Year);
            var template = library.TemplateFor(game);
            var result = Rating.Calculate(score, template.Weights);
            Title = game.Name + " · 评分详情";

            var body = new StackPanel { Margin = new Thickness(23, 17, 23, 21) };
            body.Children.Add(BuildIntro(game, template));
            body.Children.Add(BuildYears(game));
            body.Children.Add(BuildScores(game, play, score, original, template, result));
            body.Children.Add(BuildNote(game));
            body.Children.Add(BuildManagement(game, play));
            frame.SetBody(Ui.Scroll(body));
        }

        private Grid BuildIntro(Game game, WeightTemplate template)
        {
            var intro = new Grid { Margin = new Thickness(0, 0, 0, 13) };
            intro.ColumnDefinitions.Add(Ui.Pixels(89));
            intro.ColumnDefinitions.Add(Ui.Star());
            var cover = Ui.Cover(store.LoadCover(game.Cover), game.Name);
            cover.Width = 75;
            cover.Height = 100;
            cover.HorizontalAlignment = HorizontalAlignment.Left;
            Ui.Put(intro, cover, 0);
            var heading = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            heading.Children.Add(Ui.Text(game.Name, 26, Ui.Ink, true));
            var link = Ui.Button(template.Name, () => { OpenTemplateId = template.Id; Close(); }, "PlainButton");
            link.Foreground = Ui.Green;
            link.Padding = new Thickness(8, 4, 8, 4);
            var type = Ui.Text(library.Type(game.TypeId).Name + "  /", 12, Ui.Muted);
            var meta = Ui.LineUp(type, link);
            meta.Margin = new Thickness(0, 4, 0, 0);
            heading.Children.Add(meta);
            Ui.Put(intro, heading, 1);
            return intro;
        }

        private DockPanel BuildYears(Game game)
        {
            var years = new WrapPanel();
            AutomationProperties.SetName(years, "游玩年份");
            foreach (var play in game.Plays)
            {
                int value = play.Year;
                var button = Ui.Button(value.ToString(), () => SelectYear(value));
                button.Padding = new Thickness(9, 5, 9, 5);
                button.Margin = new Thickness(0, 0, 6, 4);
                if (value == year)
                {
                    button.Background = Ui.Pale;
                    button.Foreground = Ui.Green;
                }
                AutomationProperties.SetName(button, value + " 年" + (value == year ? "，当前选中" : ""));
                years.Children.Add(button);
            }
            var add = Ui.Button("＋ 添加游玩年份", () =>
            {
                var selected = Dialogs.AddPlay(this, store, library, game);
                if (!selected.HasValue) return;
                library = refresh();
                SelectYear(selected.Value);
            });
            add.VerticalAlignment = VerticalAlignment.Top;
            var row = Ui.Spread(years, add);
            row.Margin = new Thickness(0, 0, 0, 8);
            return row;
        }

        private Border BuildScores(Game game, Play play, Play score, Play original, WeightTemplate template, RatingResult result)
        {
            var panel = new StackPanel();
            var total = Ui.Number(Rating.Format(result.Total), result.Total.HasValue && !Rating.IsNotApplicable(result.Total) ? 24 : 15, Ui.Green, true);
            total.Margin = new Thickness(8, 0, 0, 0);
            var label = Ui.LineUp(Ui.Text("总分", 13, Ui.Muted, true), total);
            if (result.Total.HasValue && !Rating.IsNotApplicable(result.Total))
            {
                var denominator = Ui.Text(" / 10", 12, Ui.Muted);
                label.Children.Add(denominator);
            }
            var edit = Ui.Button(play.ReuseYear.HasValue ? "为本年单独评分" : "编辑评分", () =>
            {
                if (new ScoreWindow(this, store, game, template, score.IndependentCopy(play.Year), false).ShowDialog() != true) return;
                library = refresh();
                Render();
            });
            panel.Children.Add(new Border { Background = Ui.Pale, Padding = new Thickness(11, 7, 11, 7), Child = Ui.Spread(label, edit) });
            if (play.ReuseYear.HasValue) panel.Children.Add(BuildReuse(play, original));
            var metrics = new StackPanel { Margin = new Thickness(10, 3, 10, 4) };
            var headings = MetricGrid();
            headings.MinHeight = 26;
            Ui.Put(headings, Ui.Text("评分指标", 11, Ui.Muted), 0);
            Ui.Put(headings, Right("有效权重", 11, false), 2);
            Ui.Put(headings, Right("得分", 11, false), 3);
            metrics.Children.Add(headings);
            for (int index = 0; index < Rating.Metrics.Length; index++)
                metrics.Children.Add(MetricRow(index, score, result));
            panel.Children.Add(metrics);
            var notes = new System.Collections.Generic.List<string>();
            if (score.Detailed) notes.Add("同组适用的二级指标等权。展开指标可查看各项分数。");
            if (!result.Total.HasValue) notes.Add("有权重的适用项评完后显示总分。");
            if (Rating.IsNotApplicable(result.Total)) notes.Add("当前没有可计入总分的适用指标。");
            if (Enumerable.Range(0, template.Weights.Length).Any(index => template.Weights[index] > 0 && Rating.IsNotApplicable(result.Scores[index])))
                notes.Add("不适用项的权重由其余适用项按比例分摊。");
            if (notes.Count > 0)
            {
                var footnote = Ui.Text(string.Join(" ", notes), 11, Ui.Muted);
                footnote.Margin = new Thickness(11, 5, 11, 8);
                panel.Children.Add(footnote);
            }
            return Ui.Card(panel, Ui.Color("#F5F8EF"), 0);
        }

        private Border BuildReuse(Play play, Play original)
        {
            var information = new StackPanel();
            information.Children.Add(Ui.Text("沿用 " + play.ReuseYear.Value + " 年评分", 12, Ui.Green, true));
            information.Children.Add(Ui.Text(original == null ? "原评分暂不可用。" : "原评分变更后，本年评分也会更新。", 11, Ui.Muted));
            UIElement content = information;
            if (original != null)
            {
                var open = Ui.Button("查看 " + original.Year + " 年原评分", () => SelectYear(original.Year), "PlainButton");
                open.Foreground = Ui.Green;
                content = Ui.Spread(information, open);
            }
            return new Border { Background = Ui.Color("#EEF4E9"), BorderBrush = Ui.Line,
                BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(11, 7, 11, 7), Child = content };
        }

        private FrameworkElement MetricRow(int index, Play score, RatingResult result)
        {
            var metric = Rating.Metrics[index];
            var row = MetricGrid();
            row.MinHeight = 30;
            double? value = result.Scores[index];
            Ui.Put(row, Ui.Text(metric.Name, 13, Ui.Ink, true), 0);
            Ui.Put(row, Ui.Bar(!value.HasValue || Rating.IsNotApplicable(value) ? 0 : value.Value, 10), 1);
            Ui.Put(row, Right(Ui.Percent(result.EffectiveWeights[index]), 12, false), 2);
            Ui.Put(row, Right(Rating.Format(value), 12, true), 3);
            UIElement item = row;
            if (score.Detailed)
            {
                var subscore = new StackPanel { Margin = new Thickness(20, 0, 5, 4) };
                for (int child = 0; child < metric.Items.Length; child++)
                {
                    var pair = Ui.Spread(Ui.Text(metric.Items[child], 12, Ui.Muted), Ui.Text(Rating.Format(score.Details[index][child]), 12, Ui.Ink));
                    pair.Margin = new Thickness(0, 2, 0, 2);
                    subscore.Children.Add(pair);
                }
                // Keep the data columns aligned while the user unfolds a metric.
                var group = new Expander { Header = row, Content = subscore,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
                row.SetBinding(WidthProperty, new System.Windows.Data.Binding("ActualWidth") { Source = group, Converter = new SubtractWidth(), ConverterParameter = 22 });
                item = group;
            }
            return new Border { BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 1, 0, 0), Child = item };
        }

        private static Grid MetricGrid()
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(Ui.Pixels(120));
            row.ColumnDefinitions.Add(Ui.Star());
            row.ColumnDefinitions.Add(Ui.Pixels(62));
            row.ColumnDefinitions.Add(Ui.Pixels(68));
            return row;
        }

        private static TextBlock Right(string text, double size, bool emphasized)
        {
            var cell = Ui.Text(text, size, emphasized ? Ui.Green : Ui.Muted, emphasized);
            cell.TextAlignment = TextAlignment.Right;
            return cell;
        }

        private Border BuildNote(Game game)
        {
            var note = new StackPanel();
            var edit = Ui.Button("编辑游戏", () =>
            {
                if (Dialogs.EditGame(this, store, library, game, null) == null) return;
                library = refresh();
                Render();
            }, "PlainButton");
            edit.Foreground = Ui.Green;
            note.Children.Add(Ui.Spread(Ui.Text("我的评语", 14, Ui.Ink, true), edit));
            bool empty = string.IsNullOrEmpty(game.Note);
            note.Children.Add(Ui.Text(empty ? "还没有写评语。" : game.Note, 13, empty ? Ui.Muted : Ui.Ink));
            var card = Ui.Card(note, Ui.Paper, 10);
            card.Margin = new Thickness(0, 10, 0, 0);
            return card;
        }

        private StackPanel BuildManagement(Game game, Play play)
        {
            var controls = Ui.LineUp();
            controls.HorizontalAlignment = HorizontalAlignment.Right;
            controls.Margin = new Thickness(0, 11, 0, 0);
            if (game.Plays.Count > 1)
            {
                var removeYear = Ui.Button("删除 " + play.Year + " 年记录", () => DeleteYear(game, play), "DeleteLink");
                removeYear.Margin = new Thickness(0, 0, 8, 0);
                controls.Children.Add(removeYear);
            }
            controls.Children.Add(Ui.Button("删除游戏", () =>
            {
                if (!Dialogs.DeleteGame(this, store, game)) return;
                refresh();
                Close();
            }, "DeleteLink"));
            return controls;
        }

        private void DeleteYear(Game game, Play play)
        {
            string message = "删除「" + game.Name + "」" + play.Year + " 年的游玩记录？";
            if (game.Plays.Any(other => other.ReuseYear == play.Year))
            {
                message += play.ReuseYear.HasValue
                    ? "\n沿用本年评分的记录将改为沿用 " + play.ReuseYear.Value + " 年评分。"
                    : "\n沿用本年评分的记录会保留当前分数，并改为独立评分。";
            }
            if (!Dialogs.ConfirmDelete(this, "删除游玩记录", message, () => store.DeletePlay(game.Id, play.Year))) return;
            library = refresh();
            var remaining = library.Game(game.Id);
            SelectYear((remaining.PreviousPlay(play.Year) ?? remaining.Plays.Last()).Year);
        }

        private sealed class SubtractWidth : System.Windows.Data.IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                return Math.Max(0, (double)value - (int)parameter);
            }
            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                throw new NotSupportedException();
            }
        }
    }
}

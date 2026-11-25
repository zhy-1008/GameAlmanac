using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace GameAlmanac
{
    internal sealed class MainWindow : Window
    {
        private readonly LibraryStore store;
        private readonly LibraryLocation location;
        private readonly Border workArea = new Border();
        private readonly StackPanel sidebar = new StackPanel();
        private readonly TextBlock storagePath;
        private readonly Dictionary<int, FrameworkElement> yearAnchors = new Dictionary<int, FrameworkElement>();
        private Library library;
        private bool onShelf = true;
        private string selectedTemplate;
        private int? selectedYear;
        private ScrollViewer scroll;

        public MainWindow(LibraryStore store, LibraryLocation location)
        {
            this.store = store;
            this.location = location;
            library = store.Load();
            selectedTemplate = library.Templates[0].Id;
            Ui.SetWindow(this, "我的游戏年鉴");
            Width = Math.Min(1310, SystemParameters.WorkArea.Width - 48);
            Height = Math.Min(895, SystemParameters.WorkArea.Height - 42);
            MinWidth = 730;
            MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var shell = new Grid { Background = Ui.Base };
            shell.ColumnDefinitions.Add(Ui.Pixels(200));
            shell.ColumnDefinitions.Add(Ui.Star());
            var rail = new DockPanel { Background = Ui.Brush("Rail") };
            var footer = new StackPanel { Margin = new Thickness(12, 6, 12, 10) };
            footer.Children.Add(Navigation("存储位置", false, ChangeStorage));
            storagePath = Ui.Text(location.CurrentDirectory, 11, Ui.Muted);
            storagePath.TextWrapping = TextWrapping.NoWrap;
            storagePath.TextTrimming = TextTrimming.CharacterEllipsis;
            storagePath.Margin = new Thickness(10, 0, 0, 0);
            storagePath.ToolTip = location.CurrentDirectory;
            footer.Children.Add(storagePath);
            DockPanel.SetDock(footer, Dock.Bottom);
            rail.Children.Add(footer);
            sidebar.Margin = new Thickness(12, 17, 12, 0);
            rail.Children.Add(Ui.Scroll(sidebar));
            Ui.Put(shell, new Border { Child = rail, BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 1, 0) }, 0);
            Ui.Put(shell, workArea, 1);
            var frame = new WindowFrame(this, true);
            frame.SetBody(shell);
            Content = frame;
            Render();
        }

        internal void OpenGame(string id, int year)
        {
            var detail = new DetailWindow(this, store, library, id, year, Reload);
            detail.ShowDialog();
            if (detail.OpenTemplateId != null)
            {
                selectedTemplate = detail.OpenTemplateId;
                OpenTemplates();
            }
        }

        private Library Reload()
        {
            library = store.Load();
            Render(true);
            return library;
        }

        private void ChangeStorage()
        {
            if (!StorageWindow.Show(this, store, location)) return;
            storagePath.Text = location.CurrentDirectory;
            storagePath.ToolTip = location.CurrentDirectory;
        }

        private void Render(bool preserveOffset = false)
        {
            double oldOffset = preserveOffset && scroll != null ? scroll.VerticalOffset : 0;
            var years = library.Games.SelectMany(game => game.Plays).GroupBy(play => play.Year)
                .OrderByDescending(group => group.Key).ToArray();
            if (selectedYear.HasValue && !years.Any(group => group.Key == selectedYear.Value)) selectedYear = null;
            RenderSidebar(years);
            var page = onShelf ? BuildShelf(years.Select(group => group.Key)) : BuildTemplates();
            page.Margin = new Thickness(35, 28, 35, 36);
            page.MaxWidth = 1390;
            scroll = Ui.Scroll(page);
            workArea.Child = scroll;
            if (oldOffset > 0) scroll.ScrollToVerticalOffset(oldOffset);
        }

        private void RenderSidebar(IEnumerable<IGrouping<int, Play>> years)
        {
            sidebar.Children.Clear();
            sidebar.Children.Add(Navigation("游玩档案", onShelf && !selectedYear.HasValue, () => OpenShelf(null)));
            sidebar.Children.Add(Navigation("评分模板", !onShelf, OpenTemplates));
            var yearTitle = Ui.Text("游玩年份", 12, Ui.Muted, true);
            yearTitle.Margin = new Thickness(10, 20, 0, 6);
            sidebar.Children.Add(yearTitle);
            foreach (var group in years)
            {
                int year = group.Key;
                var label = Ui.Number(year.ToString(), 14, Ui.Ink, true);
                var count = Ui.Number(group.Count().ToString(), 12, Ui.Muted);
                var button = Navigation(year + " 年", onShelf && selectedYear == year, () => OpenShelf(year));
                button.Content = Ui.Spread(label, count);
                sidebar.Children.Add(button);
            }
            if (!years.Any())
            {
                var empty = Ui.Text("暂无游玩记录", 12, Ui.Muted);
                empty.Margin = new Thickness(10, 3, 0, 0);
                sidebar.Children.Add(empty);
            }
        }

        private static Button Navigation(string text, bool active, Action action)
        {
            var button = Ui.Button(text, action, "PlainButton");
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.Padding = new Thickness(10, 8, 10, 8);
            button.Margin = new Thickness(0, 0, 0, 3);
            if (active) button.Background = Ui.Pale;
            AutomationProperties.SetName(button, text);
            return button;
        }

        private void OpenShelf(int? year)
        {
            onShelf = true;
            selectedYear = year;
            Render();
            FrameworkElement anchor;
            if (year.HasValue && yearAnchors.TryGetValue(year.Value, out anchor))
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => anchor.BringIntoView()));
        }

        private void OpenTemplates()
        {
            onShelf = false;
            selectedYear = null;
            Render();
        }

        private static DockPanel PageHeader(string name, string summary, Button action)
        {
            var title = new StackPanel();
            title.Children.Add(Ui.Text(name, 34, Ui.Ink, true));
            var subtitle = Ui.Text(summary, 13, Ui.Muted);
            subtitle.Margin = new Thickness(0, 6, 0, 0);
            title.Children.Add(subtitle);
            action.VerticalAlignment = VerticalAlignment.Bottom;
            action.Margin = new Thickness(10, 0, 0, 2);
            var row = Ui.Spread(title, action);
            row.Margin = new Thickness(0, 0, 0, 20);
            return row;
        }

        private StackPanel BuildShelf(IEnumerable<int> years)
        {
            yearAnchors.Clear();
            var area = new StackPanel();
            var add = Ui.Button("＋ 添加游戏", () => AddGame(null), "PrimaryButton");
            area.Children.Add(PageHeader("游玩档案", library.Games.Count + " 款游戏　·　" + library.Games.Sum(game => game.Plays.Count) + " 条游玩记录", add));
            var cabinet = new StackPanel();
            foreach (int year in years)
            {
                var matching = library.Games.Where(game => game.FindPlay(year) != null).ToArray();
                var shelf = Shelf(year, matching);
                yearAnchors[year] = shelf;
                cabinet.Children.Add(shelf);
            }
            if (cabinet.Children.Count == 0)
            {
                var empty = new StackPanel { Margin = new Thickness(8, 24, 8, 26) };
                empty.Children.Add(Ui.Text("还没有游玩记录", 23, Ui.Color("#F2E9D0"), true));
                var guidance = Ui.Text("添加第一款游戏，记录游玩年份和评分。", 13, Ui.Color("#CCD9C7"));
                guidance.Margin = new Thickness(0, 7, 0, 0);
                empty.Children.Add(guidance);
                cabinet.Children.Add(new Border { Padding = new Thickness(21), Background = CabinetBackground(), Child = empty });
                cabinet.Children.Add(WoodEdge());
            }
            area.Children.Add(new Border { Child = cabinet, BorderThickness = new Thickness(1), BorderBrush = Ui.Color("#263A31"),
                Background = Ui.Color("#2C4237"), CornerRadius = new CornerRadius(7), ClipToBounds = true });
            return area;
        }

        private FrameworkElement Shelf(int year, Game[] games)
        {
            var section = new StackPanel();
            var back = new Grid { Background = CabinetBackground() };
            back.ColumnDefinitions.Add(Ui.Pixels(130));
            back.ColumnDefinitions.Add(Ui.Star());
            var label = new StackPanel { Margin = new Thickness(22, 21, 0, 0) };
            label.Children.Add(Ui.Number(year.ToString(), 33, Ui.Color("#F2E9D0"), true));
            label.Children.Add(Ui.Text(games.Length + " 款游戏", 12, Ui.Color("#CCD9C7")));
            Ui.Put(back, label, 0);
            var books = new WrapPanel { Margin = new Thickness(0, 15, 16, 1) };
            foreach (var game in games) books.Children.Add(Book(game, game.FindPlay(year)));
            Ui.Put(back, books, 1);
            section.Children.Add(back);
            section.Children.Add(WoodEdge());
            return section;
        }

        private static Brush CabinetBackground()
        {
            return new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#263A32"),
                (Color)ColorConverter.ConvertFromString("#43594A"), new Point(0, 0), new Point(1, 0.9));
        }

        private static Border WoodEdge()
        {
            return new Border { Height = 12, BorderThickness = new Thickness(0, 1, 0, 2), BorderBrush = Ui.Color("#966747"),
                Background = new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#BE9170"),
                    (Color)ColorConverter.ConvertFromString("#936547"), 90) };
        }

        private Button Book(Game game, Play play)
        {
            var title = game.Name;
            var rating = game.ScoreSource(play) ?? new Play(play.Year);
            string score = Rating.Format(Rating.Calculate(rating, library.TemplateFor(game).Weights).Total);
            var card = new StackPanel();
            var picture = new Grid { Width = 128, Height = 171 };
            picture.Children.Add(Ui.Cover(store.LoadCover(game.Cover), title));
            var medal = Ui.Number(score, score.Length > 3 ? 10 : 13, Ui.Ink, true);
            var label = new Border { Child = medal, Background = Ui.Color("#FAEED4"), CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 2, 5, 2), Margin = new Thickness(0, 7, 7, 0),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
            picture.Children.Add(label);
            card.Children.Add(picture);
            var caption = Ui.Text(title, 12, Ui.Color("#F3EFDF"), true);
            caption.TextWrapping = TextWrapping.NoWrap;
            caption.TextTrimming = TextTrimming.CharacterEllipsis;
            caption.Width = 128;
            caption.Margin = new Thickness(0, 5, 0, 4);
            card.Children.Add(caption);
            var button = Ui.Button(title, () => OpenGame(game.Id, play.Year), "BookButton");
            button.Content = card;
            button.Margin = new Thickness(0, 0, 13, 4);
            string source = play.ReuseYear.HasValue ? "，沿用 " + play.ReuseYear + " 年评分" : "";
            button.ToolTip = title + " · " + play.Year + " 年 · " + score + source;
            AutomationProperties.SetName(button, title + "，" + play.Year + " 年，" + score + source);
            var menu = new ContextMenu { Background = Ui.Paper, Foreground = Ui.Ink };
            var edit = new MenuItem { Header = "编辑游戏" };
            edit.Click += (sender, args) => { if (Dialogs.EditGame(this, store, library, game, null) != null) Reload(); };
            var delete = new MenuItem { Header = "删除游戏", Foreground = Ui.Brush("Error") };
            delete.Click += (sender, args) => { if (Dialogs.DeleteGame(this, store, game)) Reload(); };
            menu.Items.Add(edit);
            menu.Items.Add(delete);
            button.ContextMenu = menu;
            return button;
        }

        private void AddGame(int? year)
        {
            string id = Dialogs.EditGame(this, store, library, null, year);
            if (id == null) return;
            Reload();
            OpenGame(id, library.Game(id).Plays[0].Year);
        }

        private StackPanel BuildTemplates()
        {
            var page = new StackPanel();
            page.Children.Add(PageHeader("评分模板", "保存权重后，使用该模板的历年评分会自动重算。",
                Ui.Button("＋ 新建模板", NewTemplate, "PrimaryButton")));
            var layout = new Grid();
            layout.ColumnDefinitions.Add(Ui.Pixels(186));
            layout.ColumnDefinitions.Add(Ui.Star());
            var list = new StackPanel();
            list.Children.Add(Ui.Text("我的模板", 14, Ui.Ink, true));
            var counts = new Dictionary<string, TextBlock>();
            foreach (var template in library.Templates)
            {
                var heading = Ui.Text(template.Name, 13, Ui.Ink, true);
                var count = Ui.Text(TemplateCount(template.Id), 11, Ui.Muted);
                count.Margin = new Thickness(0, 2, 0, 0);
                counts.Add(template.Id, count);
                var item = new StackPanel();
                item.Children.Add(heading);
                item.Children.Add(count);
                var button = Navigation(template.Name, template.Id == selectedTemplate, () => { selectedTemplate = template.Id; Render(true); });
                button.Content = item;
                button.Margin = new Thickness(0, 5, 0, 0);
                list.Children.Add(button);
            }
            Ui.Put(layout, Ui.Card(list, padding: 12), 0);

            var selected = library.Template(selectedTemplate);
            var editor = new StackPanel();
            var remove = Ui.Button("删除模板", () => DeleteTemplate(selected), "DeleteLink");
            LimitDelete(remove, library.Templates.Count, "至少保留一个评分模板。");
            editor.Children.Add(Ui.Spread(Ui.Text(selected.Name, 19, Ui.Ink, true), remove));
            var hint = Ui.Text("调整六项一级指标的权重。", 12, Ui.Muted);
            hint.Margin = new Thickness(0, 3, 0, 12);
            editor.Children.Add(hint);
            var weights = new WeightFields(selected.Weights);
            editor.Children.Add(weights.Content);
            var status = Ui.Text("", 12, Ui.Green);
            editor.Children.Add(Ui.Spread(status, Ui.Button("保存权重", () =>
            {
                try
                {
                    store.UpdateWeights(selected.Id, weights.ReadValid());
                    library = store.Load();
                    status.Foreground = Ui.Green;
                    status.Text = "权重已保存，关联评分已更新。";
                }
                catch (Exception error) { status.Foreground = Ui.Brush("Error"); status.Text = error.Message; }
            }, "PrimaryButton")));
            var editorCard = Ui.Card(editor);
            editorCard.Margin = new Thickness(14, 0, 0, 0);
            Ui.Put(layout, editorCard, 1);
            page.Children.Add(layout);
            var mapping = BuildMapping(counts);
            mapping.Margin = new Thickness(0, 14, 0, 0);
            page.Children.Add(mapping);
            return page;
        }

        private string TemplateCount(string id)
        {
            return library.Games.Count(game => library.Type(game.TypeId).TemplateId == id) + " 款游戏";
        }

        private Border BuildMapping(Dictionary<string, TextBlock> counts)
        {
            var section = new StackPanel();
            var title = new StackPanel();
            title.Children.Add(Ui.Text("游戏类型", 17, Ui.Ink, true));
            title.Children.Add(Ui.Text("更改模板关联后，该类型的所有评分都会更新。", 12, Ui.Muted));
            section.Children.Add(Ui.Spread(title, Ui.Button("＋ 添加类型", NewType)));
            var status = Ui.Text("", 12, Ui.Green);
            status.Margin = new Thickness(0, 4, 0, 5);
            section.Children.Add(status);
            foreach (var type in library.Types)
            {
                var picker = Dialogs.TemplatePicker(library, type.TemplateId);
                AutomationProperties.SetName(picker, type.Name + "使用的评分模板");
                bool reverting = false;
                string current = type.TemplateId;
                picker.SelectionChanged += (sender, args) =>
                {
                    var selection = picker.SelectedItem as WeightTemplate;
                    if (reverting || selection == null || selection.Id == current) return;
                    string target = selection.Id;
                    try
                    {
                        store.UpdateType(type.Id, target);
                        current = target;
                        library = store.Load();
                        foreach (var entry in counts) entry.Value.Text = TemplateCount(entry.Key);
                        status.Foreground = Ui.Green;
                        status.Text = "「" + type.Name + "」已关联「" + library.Template(target).Name + "」。";
                    }
                    catch (Exception error)
                    {
                        reverting = true;
                        picker.SelectedValue = current;
                        reverting = false;
                        status.Foreground = Ui.Brush("Error");
                        status.Text = error.Message;
                    }
                };
                var row = new Grid { MinHeight = 43 };
                row.ColumnDefinitions.Add(Ui.Star(1.4));
                row.ColumnDefinitions.Add(Ui.Star());
                row.ColumnDefinitions.Add(Ui.Star(1.6));
                row.ColumnDefinitions.Add(Ui.Pixels(78));
                Ui.Put(row, Ui.Text(type.Name, 13, Ui.Ink, true), 0);
                Ui.Put(row, Ui.Text(library.Games.Count(game => game.TypeId == type.Id) + " 款游戏", 12, Ui.Muted), 1);
                picker.VerticalAlignment = VerticalAlignment.Center;
                Ui.Put(row, picker, 2);
                var remove = Ui.Button("删除", () => DeleteType(type), "DeleteLink");
                remove.Margin = new Thickness(10, 0, 0, 0);
                remove.VerticalAlignment = VerticalAlignment.Center;
                AutomationProperties.SetName(remove, "删除游戏类型 " + type.Name);
                LimitDelete(remove, library.Types.Count, "至少保留一个游戏类型。");
                Ui.Put(row, remove, 3);
                section.Children.Add(new Border { BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 1, 0, 0), Child = row });
            }
            return Ui.Card(section);
        }

        private void NewTemplate()
        {
            var name = Ui.Box("", "模板名称", 35);
            var inputs = new WeightFields(Rating.DefaultWeights());
            string created = null;
            var dialog = new FormWindow(this, "新建评分模板", "创建模板", () =>
            {
                string value = name.Text.Trim();
                if (library.Templates.Any(item => string.Equals(item.Name, value, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("这个模板名称已存在。");
                created = store.AddTemplate(value, inputs.ReadValid());
                return true;
            });
            dialog.Body.Children.Add(Ui.Field("模板名称", name));
            dialog.Body.Children.Add(inputs.Content);
            dialog.Loaded += (sender, args) => name.Focus();
            if (dialog.ShowDialog() != true) return;
            selectedTemplate = created;
            Reload();
        }

        private void NewType()
        {
            var name = Ui.Box("", "类型名称", 35);
            var template = Dialogs.TemplatePicker(library, selectedTemplate);
            var dialog = new FormWindow(this, "添加游戏类型", "添加类型", () =>
            {
                string value = name.Text.Trim();
                if (library.Types.Any(item => string.Equals(item.Name, value, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("这个类型名称已存在。");
                store.AddType(value, (string)template.SelectedValue);
                return true;
            });
            dialog.Body.Children.Add(Ui.Field("类型名称", name));
            dialog.Body.Children.Add(Ui.Field("评分模板", template));
            dialog.Loaded += (sender, args) => name.Focus();
            if (dialog.ShowDialog() == true) Reload();
        }

        private void DeleteTemplate(WeightTemplate template)
        {
            int linked = library.Types.Count(type => type.TemplateId == template.Id);
            var available = library.Templates.Where(item => item.Id != template.Id).ToArray();
            var picker = linked == 0 ? null : new ComboBox { ItemsSource = available, DisplayMemberPath = "Name", SelectedValuePath = "Id" };
            string message = "删除评分模板「" + template.Name + "」？";
            if (linked > 0) message += "\n" + linked + " 种游戏类型正在使用它。选择替代模板后，相关总分会按新权重重算。";
            if (!Dialogs.ConfirmDelete(this, "删除评分模板", message,
                () => store.DeleteTemplate(template.Id, picker == null ? null : (string)picker.SelectedValue), picker, "替代模板")) return;
            selectedTemplate = picker == null ? available[0].Id : (string)picker.SelectedValue;
            Reload();
        }

        private void DeleteType(GameType type)
        {
            int linked = library.Games.Count(game => game.TypeId == type.Id);
            var available = library.Types.Where(item => item.Id != type.Id).ToArray();
            var picker = linked == 0 ? null : new ComboBox { ItemsSource = available, DisplayMemberPath = "Name", SelectedValuePath = "Id" };
            string message = "删除游戏类型「" + type.Name + "」？";
            if (linked > 0) message += "\n" + linked + " 款游戏属于这个类型。它们会移到所选类型，并按该类型的模板重算总分。";
            if (Dialogs.ConfirmDelete(this, "删除游戏类型", message,
                () => store.DeleteType(type.Id, picker == null ? null : (string)picker.SelectedValue), picker, "替代类型")) Reload();
        }

        private static void LimitDelete(Button button, int count, string reason)
        {
            if (count > 1) return;
            button.IsEnabled = false;
            button.ToolTip = reason;
            ToolTipService.SetShowOnDisabled(button, true);
        }
    }
}

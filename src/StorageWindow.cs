using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace GameAlmanac
{
    internal static class StorageWindow
    {
        public static bool Show(Window owner, LibraryStore store, LibraryLocation location)
        {
            var path = Ui.Box(location.CurrentDirectory, "新数据文件夹", 32767);
            var currentPath = Ui.Text(location.CurrentDirectory, 12, Ui.Green);
            var message = Ui.Text("应用会将游戏、封面、评分和模板移动到新文件夹，并记住新的位置。", 13, Ui.Muted);
            message.Margin = new Thickness(0, 4, 0, 10);
            var status = Ui.Text("", 12, Ui.Green, true);
            status.Margin = new Thickness(0, 4, 0, 0);
            bool moving = false;
            bool changed = false;
            FormWindow dialog = null;

            Action<string> migrate = async destination =>
            {
                moving = true;
                dialog.Save.IsEnabled = false;
                dialog.Cancel.IsEnabled = false;
                dialog.Body.IsEnabled = false;
                status.Text = "正在迁移数据库和封面…";
                try
                {
                    bool cleaned = await Task.Run(() => store.MoveTo(destination, location.SaveDirectory));
                    moving = false;
                    changed = true;
                    if (cleaned)
                    {
                        dialog.DialogResult = true;
                        return;
                    }
                    currentPath.Text = location.CurrentDirectory;
                    path.Text = location.CurrentDirectory;
                    status.Text = "新地址已启用。旧位置仍有文件未能移除。";
                    dialog.Save.Content = "完成";
                }
                catch (Exception error)
                {
                    status.Text = "";
                    dialog.Fail(error.Message);
                }
                finally
                {
                    moving = false;
                    dialog.Save.IsEnabled = true;
                    dialog.Cancel.IsEnabled = true;
                    dialog.Body.IsEnabled = !changed;
                }
            };

            dialog = new FormWindow(owner, "存储位置", "移动并保存", () =>
            {
                if (changed) return true;
                string destination = LibraryLocation.Normalize(path.Text);
                if (string.Equals(destination, location.CurrentDirectory, StringComparison.OrdinalIgnoreCase)) return true;
                migrate(destination);
                return false;
            }, 615);
            dialog.Closing += (sender, args) => args.Cancel = moving;

            var current = new StackPanel();
            current.Children.Add(Ui.Text("当前数据位置", 13, Ui.Ink, true));
            currentPath.Margin = new Thickness(0, 5, 0, 1);
            current.Children.Add(currentPath);
            var currentCard = Ui.Card(current, Ui.Pale, 11);
            currentCard.Margin = new Thickness(0, 0, 0, 13);
            dialog.Body.Children.Add(currentCard);

            var picker = Ui.Button("浏览…", () => PickFolder(dialog, path), "PlainButton");
            picker.Margin = new Thickness(6, 0, 0, 0);
            var destinationField = new StackPanel();
            destinationField.Children.Add(Ui.Text("新数据文件夹", 13, Ui.Ink, true));
            var pathRow = Ui.Spread(path, picker);
            pathRow.Margin = new Thickness(0, 6, 0, 0);
            destinationField.Children.Add(pathRow);
            dialog.Body.Children.Add(destinationField);
            dialog.Body.Children.Add(message);
            dialog.Body.Children.Add(status);
            dialog.Loaded += (sender, args) => { path.Focus(); path.SelectAll(); };
            dialog.ShowDialog();
            return changed;
        }

        private static void PickFolder(Window dialog, TextBox path)
        {
            using (var browser = new Forms.FolderBrowserDialog())
            {
                browser.Description = "选择新的游戏年鉴数据文件夹";
                browser.ShowNewFolderButton = true;
                if (Directory.Exists(path.Text)) browser.SelectedPath = path.Text;
                if (browser.ShowDialog(new FolderDialogOwner(dialog)) == Forms.DialogResult.OK) path.Text = browser.SelectedPath;
            }
        }

        private sealed class FolderDialogOwner : Forms.IWin32Window
        {
            public IntPtr Handle { get; private set; }
            public FolderDialogOwner(Window owner) { Handle = new WindowInteropHelper(owner).Handle; }
        }
    }
}

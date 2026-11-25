using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.IO;
using System;
using AAMotion;

namespace AxisPanel
{
    /// <summary>窗口只做界面上的事：绑定数据、日志滚到最新一行、关闭时交给 ViewModel 收尾。</summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel = new MainViewModel();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _viewModel;
            string menu = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "operations.tsv");
            foreach (string line in File.Exists(menu) ? File.ReadLines(menu) : Array.Empty<string>())
            {
                string[] row = line.Split('\t');
                if (row.Length == 5)
                    Operations.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = row[1], Tag = row });
            }
            Operations.SelectedIndex = 0;
            ((INotifyCollectionChanged)_viewModel.Log).CollectionChanged += (s, e) =>
            {
                if (LogList.Items.Count > 0) LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
            };
        }

        private void SelectOperation(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!(Operations.SelectedItem is System.Windows.Controls.ComboBoxItem item)) return;
            var row = (string[])item.Tag;
            OperationHint.Text = row[2];
            Arguments.Text = row[3];
        }

        private void RunOperation(object sender, RoutedEventArgs e)
        {
            if (!(Operations.SelectedItem is System.Windows.Controls.ComboBoxItem item)) return;
            var row = (string[])item.Tag;
            if (row[4] == "1" && MessageBox.Show(this, row[1] + "?", "AAMotion",
                MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            _viewModel.RunOperation(row[0], Arguments.Text);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _viewModel.Shutdown();
            base.OnClosing(e);
        }
    }
}

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using AAMotion;

namespace AxisPanel
{
    /// <summary>状态面板中的一行：名称与当前值。</summary>
    public sealed class StatusItem : INotifyPropertyChanged
    {
        private string _value = "-";

        public StatusItem(string name) { Name = name; }

        public string Name { get; }

        public string Value
        {
            get => _value;
            set { _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// 界面绑定的数据与命令：把按钮翻译成 <see cref="AxisController"/> 的调用，
    /// 再把控制器从工作线程报告的结果切回界面线程显示。
    /// </summary>
    public sealed class MainViewModel : INotifyPropertyChanged
    {
        private readonly AxisController _controller = new AxisController();
        private readonly Dispatcher _ui = Dispatcher.CurrentDispatcher;
        private readonly DispatcherTimer _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        private bool _connected;

        public MainViewModel()
        {
            foreach (string name in AxisController.StatusNames) Status.Add(new StatusItem(name));
            _controller.Logged += line => _ui.BeginInvoke((Action)(() => Log.Add(line)));
            _controller.StatusRead += values => _ui.BeginInvoke((Action)(() =>
            {
                for (int i = 0; i < values.Length; i++) Status[i].Value = values[i];
            }));
            _controller.ConnectionChanged += ok => _ui.BeginInvoke((Action)(() => Connected = ok));
            _poll.Tick += (s, e) => _controller.Poll(SelectedAxis);

            ConnectCommand = new RelayCommand(() => _controller.Connect(Target.Trim()));
            DisconnectCommand = new RelayCommand(_controller.Disconnect);
            AutoPhaseCommand = Command("AutoPhase", a => a.AutoPhase());
            MotorOnCommand = Command("MotorOn", a => a.MotorOn = 1);
            MotorOffCommand = Command("MotorOff", a => a.MotorOn = 0);
            StopCommand = Command("Stop", a => a.Stop());
            AbortCommand = Command("Abort", a => a.Abort());
            HomeCommand = Command("Home", a => a.Home());
            MoveAbsCommand = new RelayCommand(() => SendMove(true));
            MoveRelCommand = new RelayCommand(() => SendMove(false));
            JogMinusCommand = new RelayCommand(() => Jog(-1));
            JogPlusCommand = new RelayCommand(() => Jog(1));
        }

        // 控制器 IP（出厂默认 172.1.1.101）；用仿真器时改成 firmware_sim.exe 的完整路径
        public string Target { get; set; } = "172.1.1.101";
        public string[] Axes { get; } = { "A", "B", "C", "D" };
        public int AxisIndex { get; set; }
        public string Speed { get; set; } = "2000";
        public string Accel { get; set; } = "10000";
        public string Decel { get; set; } = "10000";
        public string Value { get; set; } = "10000";
        public ObservableCollection<StatusItem> Status { get; } = new ObservableCollection<StatusItem>();
        public ObservableCollection<string> Log { get; } = new ObservableCollection<string>();

        public bool Connected
        {
            get => _connected;
            private set
            {
                _connected = value;
                _poll.IsEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LinkText));
            }
        }

        public string LinkText => _connected ? "已连接" : "未连接";

        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand AutoPhaseCommand { get; }
        public ICommand MotorOnCommand { get; }
        public ICommand MotorOffCommand { get; }
        public ICommand MoveAbsCommand { get; }
        public ICommand MoveRelCommand { get; }
        public ICommand JogMinusCommand { get; }
        public ICommand JogPlusCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand AbortCommand { get; }
        public ICommand HomeCommand { get; }

        // 按钮按下时就确定轴，命令执行时用户可能已切换
        public void RunOperation(string id, string input) => _controller.RunOperation(SelectedAxis, id, input);

        private AxisRef SelectedAxis => (AxisRef)(1 << AxisIndex);

        private ICommand Command(string name, Action<ISingleAxis> action) =>
            new RelayCommand(() => _controller.Send(name, SelectedAxis, action));

        private void SendMove(bool absolute)
        {
            if (!TryNumber(Value, out double value) || !TryNumber(Speed, out double speed) ||
                !TryNumber(Accel, out double accel) || !TryNumber(Decel, out double decel))
                return;
            // 命令被接受不等于到位：到位看状态面板中的 InTargetStat = 4
            if (absolute)
                _controller.Send("MoveAbs " + value, SelectedAxis, a => a.MoveAbs(value, speed, accel, decel));
            else
                _controller.Send("MoveRel " + value, SelectedAxis, a => a.MoveRel(value, speed, accel, decel));
        }

        private void Jog(int direction)
        {
            if (TryNumber(Speed, out double speed))
                _controller.Send("Jog " + direction * speed, SelectedAxis, a => a.Jog(direction * speed));
        }

        private bool TryNumber(string text, out double value)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)) return true;
            Log.Add("not a number: " + text);
            return false;
        }

        /// <summary>窗口关闭时调用：停止定时读状态，请求停止并断使能，再断开。</summary>
        public void Shutdown()
        {
            _poll.IsEnabled = false;
            _controller.Shutdown(SelectedAxis);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

using BGLParser;
using Pushback_Configurator.SimConnectInterface;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;

namespace Pushback_Configurator.AppUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // ------------------------------------------------------------------------------
        // FIELDS
        // ------------------------------------------------------------------------------

        public Sim sim;
        internal ActiveFiles activeFiles;

        // WndProc
        private const uint WM_USER_SIMCONNECT = 0x0402;
        private IntPtr handle;

        // ------------------------------------------------------------------------------
        // METHODS
        // ------------------------------------------------------------------------------

        /// <summary>
        /// Constructor
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            sim = new Sim(this);
            activeFiles = new ActiveFiles(((App)Application.Current).registryPath);
        }

        /// <summary>
        /// Intercept for window closing
        /// </summary>
        /// <param name="e"></param>
        protected override void OnClosing(CancelEventArgs e)
        {
            // Handle closing logic, set e.Cancel as needed
            if (sim.connected)
                sim.changeConnection(handle, WM_USER_SIMCONNECT);
        }

        /// <summary>
        /// Setup for WndProc
        /// </summary>
        /// <param name="e"></param>
        protected override void OnSourceInitialized(EventArgs e)
        {
            handle = (new WindowInteropHelper(this)).Handle;
            HwndSource src = HwndSource.FromHwnd(handle);
            src.AddHook(new HwndSourceHook(WndProc));
            sim.changeConnection(handle, WM_USER_SIMCONNECT);
        }

        /// <summary>
        /// Simconnect client will send a win32 message when there is 
        /// a packet to process. ReceiveMessage must be called to
        /// trigger the events. This model keeps simconnect processing on the main thread.
        /// </summary>
        /// <param name="hwnd"></param>
        /// <param name="msg"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <param name="handled"></param>
        /// <returns></returns>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_USER_SIMCONNECT)
            {
                sim.handleMessage();
            }
            return IntPtr.Zero;
        }

        private void customize(object sender, RoutedEventArgs e)
        {
            sim.customizePosition();
        }

        private void next(object sender, RoutedEventArgs e)
        {
            sim.cycle();
        }

        private void set(object sender, RoutedEventArgs e)
        {
            sim.set();
        }

        private void finish(object sender, RoutedEventArgs e)
        {
            sim.finish();
        }
    }
}

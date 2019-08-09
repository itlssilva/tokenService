using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Timers;


namespace TokenService
{
    public partial class ServiceToken : ServiceBase
    {
        private Timer timer;
        public ServiceToken()
        {
            InitializeComponent();
            OnStart(null);
        }

        protected override void OnStart(string[] args)
        {
            timer = new Timer(TimeSpan.FromSeconds(10).TotalMilliseconds);
            timer.Start();
            timer.Enabled = true;
            timer.Elapsed += new ElapsedEventHandler(Timer_Elapsed);
        }

        protected override void OnStop()
        {
            timer.Stop();
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                timer.Enabled = false;
                var target = new Token();
                var token = target.ObterToken();
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry($"Erro no serviço: -> {ex.Message}");
            }
            finally
            {
                timer.Enabled = true;
            }
        }
    }
}

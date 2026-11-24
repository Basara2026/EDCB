using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace EpgTimer
{
    public class ServiceItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private bool selected = false;

        private void NotifyPropertyChanged(string info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }

        public ServiceItem(EpgServiceInfo info)
        {
            ServiceInfo = info;
        }
        public EpgServiceInfo ServiceInfo
        {
            get;
            private set;
        }
        public bool IsSelected
        {
            get
            {
                return this.selected;
            }
            set
            {
                this.selected = value;
                NotifyPropertyChanged("IsSelected");
            }
        }
        public ulong ID
        {
            get { return CommonManager.Create64Key(ServiceInfo.ONID, ServiceInfo.TSID, ServiceInfo.SID); }
        }
        public string ServiceName
        {
            get { return ServiceInfo.service_name; }
        }
        /// <summary>
        /// 番組表の表示上のサービス名(設定で変更していれば変更後の名前)
        /// </summary>
        public string EpgServiceName
        {
            get { return Settings.Instance.GetServiceDisplayName(ServiceInfo.ONID, ServiceInfo.TSID, ServiceInfo.SID, ServiceInfo.service_name); }
        }
        public string NetworkName
        {
            get { return CommonManager.ConvertNetworkNameText(ServiceInfo.ONID); }
        }
    }
}

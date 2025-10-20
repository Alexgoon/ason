using CommunityToolkit.Mvvm.ComponentModel;
using Ason;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfSampleApp.AI;

namespace WpfSampleApp.ViewModels {
    public partial class ChartsViewModel : ObservableObject {
        [ObservableProperty]
        RootOperator rootOperator;
        public ChartsViewModel(RootOperator rootOperator) {
            this.rootOperator = rootOperator;
        }
    }
}

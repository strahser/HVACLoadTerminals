using System;
using HVACLoadTerminals.App.ViewModels;

namespace HVACLoadTerminals.App
{
    /// <summary>P5: массовое применение оверрайдов к выбранным помещениям
    /// (Detail-режим прототипа InsertTerminalsPandas).</summary>
    public partial class MassApplyWindow : System.Windows.Window
    {
        public MassApplyWindow(MassApplyViewModel vm)
        {
            InitializeComponent();
            DataContext = vm ?? throw new ArgumentNullException(nameof(vm));
            vm.AppliedAndClosed += () => Close();
        }
    }
}

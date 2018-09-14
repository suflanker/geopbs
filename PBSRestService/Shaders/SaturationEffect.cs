// By Steve Wortham 1/18/2011
// http://www.silverlightxap.com/controls/27/saturation-rollover-effect

using System;
using System.Windows;
using System.Windows.Media.Effects;

namespace PBS.Shaders {
    public class SaturationEffect : ShaderEffect {
        public static readonly DependencyProperty SaturationProperty = DependencyProperty.Register("Saturation", typeof(double), typeof(SaturationEffect), new PropertyMetadata(5d, PixelShaderConstantCallback(0)));
        public SaturationEffect() {
            PixelShader pixelShader = new PixelShader();
            pixelShader.UriSource = new Uri("/PBS;component/Shaders/Saturation.ps", UriKind.Relative);
            this.PixelShader = pixelShader;

            this.UpdateShaderValue(SaturationProperty);
        }
        public double Saturation {
            get { return ((double)(this.GetValue(SaturationProperty))); }
            set { this.SetValue(SaturationProperty, value); }
        }
    }
}

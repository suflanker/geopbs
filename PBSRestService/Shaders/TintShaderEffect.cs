//
//   Project:           SilverShader - Silverlight pixel shader demo application for Coding4Fun.
//
//   Changed by:        $Author$
//   Changed on:        $Date$
//   Changed in:        $Revision$
//   Project:           $URL$
//   Id:                $Id$
//
//
//   Copyright (c) 2010 Rene Schulte
//
//   This program is open source software. Please read the License.txt.
//

using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace PBS.Shaders
{
    /// <summary>Parametric tint pixel shader for Coding4Fun.</summary>
    internal class TintShaderEffect : ShaderEffect {
        public static readonly DependencyProperty InputProperty = ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(TintShaderEffect), 0);
        public static readonly DependencyProperty TintColorProperty = DependencyProperty.Register("TintColor", typeof(Color), typeof(TintShaderEffect), new PropertyMetadata(Color.FromArgb(255, 230, 179, 77), PixelShaderConstantCallback(0)));

        public Brush Input {
            get { return ((Brush)(this.GetValue(InputProperty))); }
            set { this.SetValue(InputProperty, value); }
        }

        /// <summary>The tint color.</summary>
        public Color TintColor {
            get { return ((Color)(this.GetValue(TintColorProperty))); }
            set { this.SetValue(TintColorProperty, value); }
        }

        public TintShaderEffect() {
            PixelShader pixelShader = new PixelShader();
            pixelShader.UriSource = new Uri("/PBS;component/Shaders/TintShader.ps", UriKind.Relative);
            this.PixelShader = pixelShader;

            this.UpdateShaderValue(InputProperty);
            this.UpdateShaderValue(TintColorProperty);
        }
    }
}

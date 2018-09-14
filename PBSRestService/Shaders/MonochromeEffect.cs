// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Permissive License.
// See http://www.microsoft.com/resources/sharedsource/licensingbasics/sharedsourcelicenses.mspx.
// All other rights reserved.

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace PBS.Shaders {
    internal class MonochromeEffect : ShaderEffect
    {
        /// <summary>
        /// Gets or sets the FilterColor variable within the shader.
        /// </summary>
        public static readonly DependencyProperty FilterColorProperty = DependencyProperty.Register("FilterColor", typeof(Color), typeof(MonochromeEffect), new PropertyMetadata(Colors.White, PixelShaderConstantCallback(0)));

        /// <summary>
        /// Gets or sets the Input of the shader.
        /// </summary>
        public static readonly DependencyProperty InputProperty = ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(MonochromeEffect), 0);

        /// <summary>
        /// Creates an instance and updates the shader's variables to the default values.
        /// </summary>
        public MonochromeEffect() {
            PixelShader pixelShader = new PixelShader();
            pixelShader.UriSource = new Uri("/PBS;component/Shaders/Monochrome.ps", UriKind.Relative);
            this.PixelShader = pixelShader;

            UpdateShaderValue(FilterColorProperty);
            UpdateShaderValue(InputProperty);
        }

        /// <summary>
        /// Gets or sets the FilterColor variable within the shader.
        /// </summary>
        public Color FilterColor {
            get { return (Color)GetValue(FilterColorProperty); }
            set { SetValue(FilterColorProperty, value); }
        }

        /// <summary>
        /// Gets or sets the input used in the shader.
        /// </summary>
        [BrowsableAttribute(false)]
        public Brush Input {
            get { return (Brush)GetValue(InputProperty); }
            set { SetValue(InputProperty, value); }
        }
    }
}

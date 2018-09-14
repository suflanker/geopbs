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
    /// <summary>
    /// This is the implementation of an extensible framework ShaderEffect which loads
    /// a shader model 2 pixel shader. Dependecy properties declared in this class are mapped
    /// to registers as defined in the *.ps file being loaded below.
    /// </summary>
    internal class InvertColorEffect : ShaderEffect
    {
        /// <summary>
        /// Gets or sets the Input of the shader.
        /// </summary>
        public static readonly DependencyProperty InputProperty = ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(InvertColorEffect), 0);

        /// <summary>
        /// Creates an instance and updates the shader's variables to the default values.
        /// </summary>
        public InvertColorEffect() {
            PixelShader pixelShader = new PixelShader();
            pixelShader.UriSource = new Uri("/PBS;component/Shaders/InvertColor.ps", UriKind.Relative);
            this.PixelShader = pixelShader;

            this.UpdateShaderValue(InvertColorEffect.InputProperty);
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

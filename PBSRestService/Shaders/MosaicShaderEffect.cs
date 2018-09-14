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

namespace PBS.Shaders {
    /// <summary>Mosaic Shader for Coding4Fun.</summary>
    public class MosaicShaderEffect : ShaderEffect {
        public static readonly DependencyProperty InputProperty = ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(MosaicShaderEffect), 0);
        public static readonly DependencyProperty BlockCountProperty = DependencyProperty.Register("BlockCount", typeof(float), typeof(MosaicShaderEffect), new PropertyMetadata(((float)(50F)), PixelShaderConstantCallback(0)));
        public static readonly DependencyProperty MinProperty = DependencyProperty.Register("Min", typeof(float), typeof(MosaicShaderEffect), new PropertyMetadata(((float)(0.0F)), PixelShaderConstantCallback(1)));
        public static readonly DependencyProperty MaxProperty = DependencyProperty.Register("Max", typeof(float), typeof(MosaicShaderEffect), new PropertyMetadata(((float)(0.75F)), PixelShaderConstantCallback(2)));
        public static readonly DependencyProperty AspectRatioProperty = DependencyProperty.Register("AspectRatio", typeof(float), typeof(MosaicShaderEffect), new PropertyMetadata(((float)(1.5F)), PixelShaderConstantCallback(3)));

        public Brush Input {
            get { return ((Brush)(GetValue(InputProperty))); }
            set { SetValue(InputProperty, value); }
        }

        /// <summary>The number pixel blocks.</summary>
        public float BlockCount {
            get { return ((float)(GetValue(BlockCountProperty))); }
            set { SetValue(BlockCountProperty, value); }
        }

        /// <summary>The rounding of a pixel block.</summary>
        public float Min {
            get { return ((float)(GetValue(MinProperty))); }
            set { SetValue(MinProperty, value); }
        }

        /// <summary>The rounding of a pixel block.</summary>
        public float Max {
            get { return ((float)(GetValue(MaxProperty))); }
            set { SetValue(MaxProperty, value); }
        }

        /// <summary>The aspect ratio of the image.</summary>
        public float AspectRatio {
            get { return ((float)(GetValue(AspectRatioProperty))); }
            set { SetValue(AspectRatioProperty, value); }
        }

        public MosaicShaderEffect() {
            var pixelShader = new PixelShader {
                UriSource =
                   new Uri("/PBS;component/Shaders/MosaicShader.ps", UriKind.Relative)
            };
            PixelShader = pixelShader;

            UpdateShaderValue(InputProperty);
            UpdateShaderValue(BlockCountProperty);
            UpdateShaderValue(MinProperty);
            UpdateShaderValue(MaxProperty);
            UpdateShaderValue(AspectRatioProperty);
        }
    }
}

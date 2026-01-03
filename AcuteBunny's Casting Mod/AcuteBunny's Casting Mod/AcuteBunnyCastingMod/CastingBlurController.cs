using System;
using System.ComponentModel.DataAnnotations;
using UnityEngine;

namespace AcuteBunnyCastingMod {
    public class CameraBlurController : MonoBehaviour {
        private void Awake() {
            Shader shader = Shader.Find("Hidden/KawaseBlur");
            bool flag = shader == null;
            if(flag) {
                this.blurMaterial = new Material("\r\n            Shader \"Hidden/KawaseBlur\" {\r\n                Properties {\r\n                    _MainTex (\"Texture\", 2D) = \"white\" {}\r\n                    _BlurAmount (\"Blur Amount\", Range(0,1)) = 0\r\n                }\r\n                SubShader {\r\n                    Cull Off ZWrite Off ZTest Always\r\n                    Pass {\r\n                        CGPROGRAM\r\n                        #pragma vertex vert\r\n                        #pragma fragment frag\r\n                        #include \"UnityCG.cginc\"\r\n\r\n                        struct appdata {\r\n                            float4 vertex : POSITION;\r\n                            float2 uv : TEXCOORD0;\r\n                        };\r\n\r\n                        struct v2f {\r\n                            float2 uv : TEXCOORD0;\r\n                            float4 vertex : SV_POSITION;\r\n                        };\r\n\r\n                        sampler2D _MainTex;\r\n                        float4 _MainTex_TexelSize;\r\n                        float _BlurAmount;\r\n\r\n                        v2f vert (appdata v) {\r\n                            v2f o;\r\n                            o.vertex = UnityObjectToClipPos(v.vertex);\r\n                            o.uv = v.uv;\r\n                            return o;\r\n                        }\r\n                        \r\n                        fixed4 frag (v2f i) : SV_Target {\r\n                            float2 offset = _MainTex_TexelSize.xy * _BlurAmount * 1.5;\r\n                            \r\n                            fixed4 col = tex2D(_MainTex, i.uv) * 0.2;\r\n                            col += tex2D(_MainTex, i.uv + float2(offset.x, offset.y)) * 0.2;\r\n                            col += tex2D(_MainTex, i.uv + float2(-offset.x, offset.y)) * 0.2;\r\n                            col += tex2D(_MainTex, i.uv + float2(offset.x, -offset.y)) * 0.2;\r\n                            col += tex2D(_MainTex, i.uv + float2(-offset.x, -offset.y)) * 0.2;\r\n                            \r\n                            return col;\r\n                        }\r\n                        ENDCG\r\n                    }\r\n                }\r\n            }");
            } else {
                this.blurMaterial = new Material(shader);
            }
            bool flag2 = this.blurMaterial == null || this.blurMaterial.shader == null;
            if(flag2) {
                CastingMod.Log.LogError("Failed to create the camera blur material. The blur effect will be disabled.");
            } else {
                this.blurMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination) {
            bool flag = this.BlurAmount > 0.01f && this.blurMaterial != null;
            if(flag) {
                this.blurMaterial.SetFloat(CameraBlurController.BlurAmountID, this.BlurAmount);
                int width = source.width / 4;
                int height = source.height / 4;
                RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, source.format);
                Graphics.Blit(source, temporary, this.blurMaterial);
                Graphics.Blit(temporary, destination, this.blurMaterial);
                RenderTexture.ReleaseTemporary(temporary);
            } else {
                Graphics.Blit(source, destination);
            }
        }

        private void OnDestroy() {
            bool flag = this.blurMaterial != null;
            if(flag) {
                UnityEngine.Object.Destroy(this.blurMaterial);
            }
        }

        [Range(0f, 1f)]
        public float BlurAmount = 0f;

        private Material blurMaterial;

        private static readonly int BlurAmountID = Shader.PropertyToID("_BlurAmount");

        private const string BlurShaderCode = "\r\n            Shader \"Hidden/KawaseBlur\" {\r\n                Properties {\r\n                    _MainTex (\"Texture\", 2D) = \"white\" {}\r\n                    _BlurAmount (\"Blur Amount\", Range(0,1)) = 0\r\n                }\r\n                SubShader {\r\n                    Cull Off ZWrite Off ZTest Always\r\n                    Pass {\r\n                        CGPROGRAM\r\n                        #pragma vertex vert\r\n                        #pragma fragment frag\r\n                        #include \"UnityCG.cginc\"\r\n\r\n                        struct appdata {\r\n                            float4 vertex : POSITION;\r\n                            float2 uv : TEXCOORD0;\r\n                        };\r\n\r\n                        struct v2f {\r\n                            float2 uv : TEXCOORD0;\r\n                            float4 vertex : SV_POSITION;\r\n                        };\r\n\r\n                        sampler2D _MainTex;\r\n                        float4 _MainTex_TexelSize;\r\n                        float _BlurAmount;\r\n\r\n                        v2f vert (appdata v) {\r\n                            v2f o;\r\n                            o.vertex = UnityObjectToClipPos(v.vertex);\r\n                            o.uv = v.uv;\r\n                            return o;\r\n                        }\r\n                        \r\n                        fixed4 frag (v2f i) : SV_Target {\r\n                            float2 offset = _MainTex_TexelSize.xy * _BlurAmount * 1.5;\r\n                            \r\n                            fixed4 col = tex2D(_MainTex, i.uv) * 0.2;\r\n                            col += tex2D(_MainTex, i.uv + float2(offset.x, offset.y)) * 0.2;\r\n                            col += tex2D(_MainTex, i.uv + float2(-offset.x, offset.y)) * 0.2;\r\n                            col += tex2D(_MainTex, i.uv + float2(offset.x, -offset.y)) * 0.2;\r\n                            col += tex2D(_MainTex, i.uv + float2(-offset.x, -offset.y)) * 0.2;\r\n                            \r\n                            return col;\r\n                        }\r\n                        ENDCG\r\n                    }\r\n                }\r\n            }";
    }
}

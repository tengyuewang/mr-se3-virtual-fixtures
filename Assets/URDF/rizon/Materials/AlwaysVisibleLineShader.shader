Shader "Custom/AlwaysVisibleLineShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue" = "Overlay" }
        Pass
        {
            ZTest Always
            Cull Off
            ZWrite On

            SetTexture[_MainTex] { combine texture * primary }
        }
    }
}

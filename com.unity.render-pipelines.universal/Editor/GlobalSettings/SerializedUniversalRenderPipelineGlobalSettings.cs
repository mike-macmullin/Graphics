using UnityEngine;
using System.Linq;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

namespace UnityEditor.Rendering.Universal
{
    class SerializedUniversalRenderPipelineGlobalSettings
    {
        public SerializedObject serializedObject;
        private List<UniversalRenderPipelineGlobalSettings> serializedSettings = new List<UniversalRenderPipelineGlobalSettings>();

        public SerializedProperty lightLayerName0;
        public SerializedProperty lightLayerName1;
        public SerializedProperty lightLayerName2;
        public SerializedProperty lightLayerName3;
        public SerializedProperty lightLayerName4;
        public SerializedProperty lightLayerName5;
        public SerializedProperty lightLayerName6;
        public SerializedProperty lightLayerName7;

        public SerializedProperty decalLayerName0;
        public SerializedProperty decalLayerName1;
        public SerializedProperty decalLayerName2;
        public SerializedProperty decalLayerName3;
        public SerializedProperty decalLayerName4;
        public SerializedProperty decalLayerName5;
        public SerializedProperty decalLayerName6;
        public SerializedProperty decalLayerName7;

        public SerializedProperty stripDebugVariants;
        public SerializedProperty stripUnusedPostProcessingVariants;
        public SerializedProperty stripUnusedVariants;

        public SerializedUniversalRenderPipelineGlobalSettings(SerializedObject serializedObject)
        {
            this.serializedObject = serializedObject;

            // do the cast only once
            foreach (var currentSetting in serializedObject.targetObjects)
            {
                if (currentSetting is UniversalRenderPipelineGlobalSettings urpSettings)
                    serializedSettings.Add(urpSettings);
                else
                    throw new System.Exception($"Target object has an invalid object, objects must be of type {typeof(UniversalRenderPipelineGlobalSettings)}");
            }


            lightLayerName0 = serializedObject.Find((UniversalRenderPipelineGlobalSettings s) => s.lightLayerName0);
            lightLayerName1 = serializedObject.Find((UniversalRenderPipelineGlobalSettings s) => s.lightLayerName1);
            lightLayerName2 = serializedObject.Find((UniversalRenderPipelineGlobalSettings s) => s.lightLayerName2);
            lightLayerName3 = serializedObject.Find((UniversalRenderPipelineGlobalSettings s) => s.lightLayerName3);
            lightLayerName4 = serializedObject.Find((UniversalRenderPipelineGlobalSettings s) => s.lightLayerName4);
            lightLayerName5 = serializedObject.Find((UniversalRenderPipelineGlobalSettings s) => s.lightLayerName5);
            lightLayerName6 = serializedObject.Find((UniversalRenderPipelineGlobalSettings s) => s.lightLayerName6);
            lightLayerName7 = serializedObject.Find((UniversalRenderPipelineGlobalSettings s) => s.lightLayerName7);

            decalLayerName0 = serializedObject.Find((UniversalRenderPipelineGlobalSettings s) => s.decalLayerName0);
            decalLayerName1 = serializedObject.Find((UniversalRenderPipelineGlobalSettings s) => s.decalLayerName1);
            decalLayerName2 = serializedObject.Find((UniversalRenderPipelineGlobalSettings s) => s.decalLayerName2);
            decalLayerName3 = serializedObject.Find((UniversalRenderPipelineGlobalSettings s) => s.decalLayerName3);
            decalLayerName4 = serializedObject.Find((UniversalRenderPipelineGlobalSettings s) => s.decalLayerName4);
            decalLayerName5 = serializedObject.Find((UniversalRenderPipelineGlobalSettings s) => s.decalLayerName5);
            decalLayerName6 = serializedObject.Find((UniversalRenderPipelineGlobalSettings s) => s.decalLayerName6);
            decalLayerName7 = serializedObject.Find((UniversalRenderPipelineGlobalSettings s) => s.decalLayerName7);

            stripDebugVariants = serializedObject.FindProperty("m_StripDebugVariants");
            stripUnusedPostProcessingVariants = serializedObject.FindProperty("m_StripUnusedPostProcessingVariants");
            stripUnusedVariants = serializedObject.FindProperty("m_StripUnusedVariants");
        }
    }
}

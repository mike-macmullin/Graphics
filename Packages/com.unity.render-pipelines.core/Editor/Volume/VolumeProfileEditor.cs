using System.Linq;
using UnityEngine.Rendering;

namespace UnityEditor.Rendering
{
    [CustomEditor(typeof(VolumeProfile))]
    [SupportedOnRenderPipeline]
    sealed class VolumeProfileEditor : Editor
    {
        VolumeComponentListEditor m_ComponentList;

        void OnEnable()
        {
            m_ComponentList = new VolumeComponentListEditor(this);
            var volumeProfile = target as VolumeProfile;
            m_ComponentList.Init(volumeProfile, serializedObject);

            if (volumeProfile == VolumeManager.instance.globalDefaultProfile)
                VolumeProfileUtils.EnsureOverridesForAllComponents(volumeProfile);
        }

        void OnDisable()
        {
            if (m_ComponentList != null)
                m_ComponentList.Clear();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            m_ComponentList.OnGUI();

            EditorGUILayout.Space();
            if (m_ComponentList.hasHiddenVolumeComponents)
                EditorGUILayout.HelpBox("There are Volume Components that are hidden in this asset because they are incompatible with the current active Render Pipeline. Change the active Render Pipeline to see them.", MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }
    }
}

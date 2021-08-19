using System;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VRC.Examples.Obstacle
{
    [ExecuteInEditMode]
    public class CheckpointGizmo : MonoBehaviour
    {
#if UNITY_EDITOR

        private static ObstacleCourseAsset _courseAsset;
        private GUIStyle _sceneViewCheckpointLabelStyle;
        private Texture2D _sceneViewTooltipBg;
        
        private void OnEnable()
        {
            if (_courseAsset == null)
            {
                RefreshCourseAsset(); 
            }
            
            if (_courseAsset != null)
            {
               _sceneViewTooltipBg = new Texture2D(1, 1, TextureFormat.RGBAFloat, false); 
               _sceneViewTooltipBg.SetPixel(0, 0, new Color(0, 0, 0, 0.75f));
               _sceneViewTooltipBg.Apply(); // not sure if this is necessary
               
               _sceneViewCheckpointLabelStyle = new GUIStyle(GUIStyle.none); 
               _sceneViewCheckpointLabelStyle.fontSize = 14;
               _sceneViewCheckpointLabelStyle.normal.textColor = new Color(86, 156, 214);
               _sceneViewCheckpointLabelStyle.normal.background = _sceneViewTooltipBg;
               _sceneViewCheckpointLabelStyle.alignment = TextAnchor.MiddleCenter;
            }
        }

        private void OnDrawGizmos()
        {
            if (_courseAsset != null && _courseAsset.showCheckpointLabels)
            {
                var position = transform.position;
                position.y += _courseAsset.checkpointLabelOffset;
                Handles.Label(position, gameObject.name, _sceneViewCheckpointLabelStyle);
            }
        }

        private void RefreshCourseAsset()
        {
            if (TryFindCourseAsset(out _courseAsset))
            {
                // all good!
            }
            else
            {
                _courseAsset = null;
                this.enabled = false;
            }
        }
        
        private bool TryFindCourseAsset(out ObstacleCourseAsset asset)
        {
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject g in roots)
            {
                ObstacleCourseData d = g.transform.GetComponentInChildren<ObstacleCourseData>(true);
                if (d != null)
                {
                    asset = d.asset;
                    return true;
                }
            }

            asset = null;
            return false;
        }
#endif
    }

}
using System;
using System.Collections.Generic;
using System.Linq;
using SharpFlux;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Reflect;
using UnityEngine.Reflect.Viewer.Pipeline;

namespace Unity.Reflect.Viewer.UI
{
    public class UISelectionController : MonoBehaviour
    {
#pragma warning disable CS0649
        [SerializeField] float m_Tolerance;
#pragma warning restore CS0649

        readonly List<Tuple<GameObject, RaycastHit>> m_Results = new List<Tuple<GameObject, RaycastHit>>();

        HighlightFilterInfo m_CurrentHighlightFilter;
        ObjectSelectionInfo m_CurrentObjectSelectionInfo;
        GameObject m_CurrentSelectedGameObject;

        Vector2? m_PreviousScreenPoint;
        int m_CurrentLayer;
        bool m_SelectMode;
        bool m_Pressed;
        bool m_IsBimInfoOpen;
        bool m_IsMetadataFilterActive;

        ISpatialPicker<Tuple<GameObject, RaycastHit>> m_ObjectPicker;
        Camera m_Camera;

        void Start()
        {
            UIStateManager.stateChanged += OnStateDataChanged;
            UIStateManager.projectStateChanged += OnProjectStateDataChanged;
        }

        void Update()
        {
            if (!m_SelectMode)
                return;

            if (!m_Pressed && IsTouchStart())
            {
                m_Pressed = true;
            }

            if(m_Pressed)
            {
                var touchEndPosition = IsTouchEnd();
                if (touchEndPosition != Vector2.zero)
                {
                    TapScreen(touchEndPosition);
                    m_Pressed = false;
                }
            }
        }

        void OnStateDataChanged(UIStateData data)
        {
            m_SelectMode = data.toolState.activeTool == ToolType.SelectTool;
            m_IsBimInfoOpen = data.activeSubDialog == DialogType.BimInfo;

            UpdateSelectedObjectHighlight();
        }

        void OnProjectStateDataChanged(UIProjectStateData data)
        {
            m_ObjectPicker = data.objectPicker;
            m_IsMetadataFilterActive = !string.IsNullOrEmpty(data.highlightFilter.filterKey) || !string.IsNullOrEmpty(data.highlightFilter.groupKey);

            if (data.objectSelectionInfo != m_CurrentObjectSelectionInfo)
            {
                ResetSelectedObjectHighlight();

                var selectedObject = data.objectSelectionInfo.CurrentSelectedObject();
                if (selectedObject != null)
                {
                    var metadata = selectedObject.GetComponent<Metadata>();
                    if (metadata == null)
                    {
                        while (selectedObject.transform.parent != null)
                        {
                            selectedObject = selectedObject.transform.parent.gameObject;
                            metadata = selectedObject.GetComponent<Metadata>();
                            if (metadata != null)
                                break;
                        }
                    }

                    if (metadata != null)
                    {
                        m_CurrentLayer = selectedObject.layer;
                    }
                    else
                    {
                        selectedObject = null;
                    }
                }

                m_CurrentSelectedGameObject = selectedObject;
                m_CurrentObjectSelectionInfo = data.objectSelectionInfo;

                UpdateSelectedObjectHighlight();
            }

            if (data.highlightFilter != m_CurrentHighlightFilter)
            {
                if (m_CurrentSelectedGameObject != null)
                {
                    m_CurrentLayer = m_CurrentSelectedGameObject.layer;
                    UpdateSelectedObjectHighlight();
                }

                m_CurrentHighlightFilter = data.highlightFilter;
            }
        }

        void ResetSelectedObjectHighlight()
        {
            if (m_CurrentSelectedGameObject != null)
            {
                SetLayer(m_CurrentSelectedGameObject, m_CurrentLayer);
            }
        }

        void UpdateSelectedObjectHighlight()
        {
            if (m_CurrentSelectedGameObject != null)
            {
                if (ShouldDisplaySelection())
                {
                    SetLayer(m_CurrentSelectedGameObject, "Selection");
                }
                else
                {
                    SetLayer(m_CurrentSelectedGameObject, m_CurrentLayer);
                }
            }
        }

        bool ShouldDisplaySelection()
        {
            return m_IsMetadataFilterActive || m_IsBimInfoOpen;
        }

        static void SetLayer(GameObject obj, string layerName)
        {
            SetLayer(obj, LayerMask.NameToLayer(layerName));
        }

        static void SetLayer(GameObject obj, int layer)
        {
            obj.SetLayerRecursively(layer);
        }

        void TapScreen(Vector2 screenPoint)
        {
            var info = new ObjectSelectionInfo();

            if (m_PreviousScreenPoint.HasValue && (screenPoint - m_PreviousScreenPoint.Value).magnitude <= m_Tolerance)
            {
                info.selectedObjects = m_CurrentObjectSelectionInfo.selectedObjects;
                info.currentIndex = m_CurrentObjectSelectionInfo.selectedObjects.Count == 0
                    ? 0
                    : (m_CurrentObjectSelectionInfo.currentIndex + 1) %
                    m_CurrentObjectSelectionInfo.selectedObjects.Count;
            }
            else
            {
                if (m_Camera == null || !m_Camera.gameObject.activeInHierarchy)
                {
                    m_Camera = Camera.main;
                    if (m_Camera == null)
                    {
                        Debug.LogError($"[{nameof(UISelectionController)}] active main camera not found!");
                        return;
                    }
                }

                m_ObjectPicker.Pick(m_Camera.ScreenPointToRay(screenPoint), m_Results);
                // send a copy of the list to preserve previous selection info
                info.selectedObjects = m_Results.Select(x => x.Item1).ToList();
                info.currentIndex = 0;
            }

            m_PreviousScreenPoint = screenPoint;
            UIStateManager.current.Dispatcher.Dispatch(Payload<ActionTypes>.From(ActionTypes.SelectObjects, info));
        }

        static bool IsTouchStart()
        {
            var id = -1;
            var pressed = false;


            if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                id = Input.GetTouch(0).fingerId;
                pressed = true;
            }

            if (!pressed)
            {
                pressed = Input.GetMouseButtonDown(0);
            }

            if (pressed)
            {
                pressed = !EventSystem.current.IsPointerOverGameObject(id);
            }

            return pressed;
        }

        static Vector2 IsTouchEnd()
        {
            var touchEnd = Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Ended;

            if (touchEnd)
            {
                return Input.GetTouch(0).position;
            }

            touchEnd = Input.GetMouseButtonUp(0);
            if (touchEnd)
            {
                return Input.mousePosition;
            }

            return Vector2.zero;
        }
    }
}



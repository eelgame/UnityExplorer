﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MelonLoader;
using UnhollowerRuntimeLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Explorer
{
    public class GameObjectWindow : UIWindow
    {
        public override string Title => WindowManager.TabView
            ? $"<color=cyan>[G]</color> {m_object.name}"
            : $"GameObject Inspector ({m_object.name})";

        public GameObject m_object;

        // gui element holders
        private string m_name;
        private string m_scene;

        private Transform[] m_children;
        private Vector2 m_transformScroll = Vector2.zero;
        private PageHelper ChildPages = new PageHelper();

        private Component[] m_components;
        private Vector2 m_compScroll = Vector2.zero;
        private PageHelper CompPages = new PageHelper();

        private readonly Vector3[] m_cachedInput = new Vector3[3];
        private float m_translateAmount = 0.3f;
        private float m_rotateAmount = 50f;
        private float m_scaleAmount = 0.1f;
        private bool m_freeze;
        private Vector3 m_frozenPosition;
        private Quaternion m_frozenRotation;
        private Vector3 m_frozenScale;
        private bool m_autoApplyTransform;
        private bool m_autoUpdateTransform;
        private bool m_localContext;

        private readonly List<Component> m_cachedDestroyList = new List<Component>();
        //private string m_addComponentInput = "";

        private string m_setParentInput = "Enter a GameObject name or path";

        public bool GetObjectAsGameObject()
        {
            if (Target == null)
            {
                MelonLogger.Log("Target is null!");
                return false;
            }

            var targetType = Target.GetType();

            if (targetType == typeof(GameObject))
            {
                m_object = Target as GameObject;
                return true;
            }
            else if (targetType == typeof(Transform))
            {
                m_object = (Target as Transform).gameObject;
                return true;
            }

            MelonLogger.Log("Error: Target is null or not a GameObject/Transform!");
            DestroyWindow();
            return false;
        }

        public override void Init()
        {
            if (!GetObjectAsGameObject())
            {
                return;
            }

            m_name = m_object.name;
            m_scene = string.IsNullOrEmpty(m_object.scene.name) 
                        ? "None (Asset/Resource)" 
                        : m_object.scene.name;

            CacheTransformValues();

            Update();
        }

        private void CacheTransformValues()
        {
            if (m_localContext)
            {
                m_cachedInput[0] = m_object.transform.localPosition;
                m_cachedInput[1] = m_object.transform.localEulerAngles;
            }
            else
            {
                m_cachedInput[0] = m_object.transform.position;
                m_cachedInput[1] = m_object.transform.eulerAngles;
            }
            m_cachedInput[2] = m_object.transform.localScale;
        }

        public override void Update()
        {
            try
            {
                if (!m_object && !GetObjectAsGameObject())
                {
                    throw new Exception("Object is null!");
                }

                if (m_freeze)
                {
                    if (m_localContext)
                    {
                        m_object.transform.localPosition = m_frozenPosition;
                        m_object.transform.localRotation = m_frozenRotation;
                    }
                    else
                    {
                        m_object.transform.position = m_frozenPosition;
                        m_object.transform.rotation = m_frozenRotation;
                    }
                    m_object.transform.localScale = m_frozenScale;
                }

                var list = new List<Transform>();
                for (int i = 0; i < m_object.transform.childCount; i++)
                {
                    list.Add(m_object.transform.GetChild(i));
                }
                list.Sort((a, b) => b.childCount.CompareTo(a.childCount));
                m_children = list.ToArray();

                ChildPages.ItemCount = m_children.Length;

                var list2 = new List<Component>();
                foreach (var comp in m_object.GetComponents(ReflectionHelpers.ComponentType))
                {
                    var ilType = comp.GetIl2CppType();
                    if (ilType == ReflectionHelpers.TransformType)
                    {
                        continue;
                    }

                    list2.Add(comp);
                }
                m_components = list2.ToArray();

                CompPages.ItemCount = m_components.Length;
            }
            catch (Exception e)
            {
                DestroyOnException(e);
            }
        }

        private void DestroyOnException(Exception e)
        {
            MelonLogger.Log($"Exception drawing GameObject Window: {e.GetType()}, {e.Message}");
            DestroyWindow();
        }

        private void InspectGameObject(Transform obj)
        {
            var window = WindowManager.InspectObject(obj, out bool created);
            
            if (created)
            {
                window.m_rect = new Rect(this.m_rect.x, this.m_rect.y, this.m_rect.width, this.m_rect.height);
                DestroyWindow();
            }
        }

        private void ReflectObject(Il2CppSystem.Object obj)
        {
            var window = WindowManager.InspectObject(obj, out bool created);

            if (created)
            {
                if (this.m_rect.x <= (Screen.width - this.m_rect.width - 100))
                {
                    window.m_rect = new Rect(
                        this.m_rect.x + this.m_rect.width + 20,
                        this.m_rect.y,
                        550,
                        700);
                }
                else
                {
                    window.m_rect = new Rect(this.m_rect.x + 50, this.m_rect.y + 50, 550, 700);
                }
            }
        }

        public override void WindowFunction(int windowID)
        {
            try
            {
                var rect = WindowManager.TabView ? TabViewWindow.Instance.m_rect : this.m_rect;

                if (!WindowManager.TabView)
                {
                    Header();
                    GUILayout.BeginArea(new Rect(5, 25, rect.width - 10, rect.height - 35), GUI.skin.box);
                }

                scroll = GUIUnstrip.BeginScrollView(scroll);

                GUILayout.BeginHorizontal(null);
                GUILayout.Label("Scene: <color=cyan>" + (m_scene == "" ? "n/a" : m_scene) + "</color>", null);
                if (m_scene == UnityHelpers.ActiveSceneName)
                {
                    if (GUILayout.Button("<color=#00FF00>Send to Scene View</color>", new GUILayoutOption[] { GUILayout.Width(150) }))
                    {
                        ScenePage.Instance.SetTransformTarget(m_object.transform);
                        MainMenu.SetCurrentPage(0);
                    }
                }
                if (GUILayout.Button("Reflection Inspect", new GUILayoutOption[] { GUILayout.Width(150) }))
                {
                    WindowManager.InspectObject(Target, out _, true);
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal(null);
                GUILayout.Label("Path:", new GUILayoutOption[] { GUILayout.Width(50) });
                string pathlabel = m_object.transform.GetGameObjectPath();
                if (m_object.transform.parent != null)
                {
                    if (GUILayout.Button("<-", new GUILayoutOption[] { GUILayout.Width(35) }))
                    {
                        InspectGameObject(m_object.transform.parent);
                    }
                }
                GUILayout.TextArea(pathlabel, null);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal(null);
                GUILayout.Label("Name:", new GUILayoutOption[] { GUILayout.Width(50) });
                GUILayout.TextArea(m_name, null);
                GUILayout.EndHorizontal();

                // --- Horizontal Columns section ---
                GUILayout.BeginHorizontal(null);

                GUILayout.BeginVertical(new GUILayoutOption[] { GUILayout.Width(rect.width / 2 - 17) });
                TransformList(rect);
                GUILayout.EndVertical();

                GUILayout.BeginVertical(new GUILayoutOption[] { GUILayout.Width(rect.width / 2 - 17) });
                ComponentList(rect);
                GUILayout.EndVertical();

                GUILayout.EndHorizontal(); // end horiz columns

                GameObjectControls();

                GUIUnstrip.EndScrollView();

                if (!WindowManager.TabView)
                {
                    m_rect = ResizeDrag.ResizeWindow(rect, windowID);

                    GUILayout.EndArea();
                }
            }
            catch (Exception e)
            {
                DestroyOnException(e);
            }
        }

        private void TransformList(Rect m_rect)
        {
            GUILayout.BeginVertical(GUI.skin.box, null);
            m_transformScroll = GUIUnstrip.BeginScrollView(m_transformScroll);

            GUILayout.Label("<b><size=15>Children</size></b>", null);

            GUILayout.BeginHorizontal(null);
            ChildPages.DrawLimitInputArea();

            if (ChildPages.ItemCount > ChildPages.ItemsPerPage)
            {
                ChildPages.CurrentPageLabel();

                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal(null);

                if (GUILayout.Button("< Prev", new GUILayoutOption[] { GUILayout.Width(80) }))
                {
                    ChildPages.TurnPage(Turn.Left, ref this.m_transformScroll);
                }
                if (GUILayout.Button("Next >", new GUILayoutOption[] { GUILayout.Width(80) }))
                {
                    ChildPages.TurnPage(Turn.Right, ref this.m_transformScroll);
                }
            }
            GUILayout.EndHorizontal();

            if (m_children != null && m_children.Length > 0)
            {
                int start = ChildPages.CalculateOffsetIndex();

                for (int j = start; (j < start + ChildPages.ItemsPerPage && j < ChildPages.ItemCount); j++)
                {
                    var obj = m_children[j];

                    if (!obj)
                    {
                        GUILayout.Label("null", null);
                        continue;
                    }

                    UIHelpers.GOButton(obj.gameObject, InspectGameObject, false, m_rect.width / 2 - 80);
                }
            }
            else
            {
                GUILayout.Label("<i>None</i>", null);
            }

            GUIUnstrip.EndScrollView();
            GUILayout.EndVertical();
        }

        private void ComponentList(Rect m_rect)
        {
            GUILayout.BeginVertical(GUI.skin.box, null);
            m_compScroll = GUIUnstrip.BeginScrollView(m_compScroll);
            GUILayout.Label("<b><size=15>Components</size></b>", null);

            GUILayout.BeginHorizontal(null);
            CompPages.DrawLimitInputArea();

            if (CompPages.ItemCount > CompPages.ItemsPerPage)
            {
                CompPages.CurrentPageLabel();

                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal(null);

                if (GUILayout.Button("< Prev", new GUILayoutOption[] { GUILayout.Width(80) }))
                {
                    CompPages.TurnPage(Turn.Left, ref this.m_compScroll);
                }
                if (GUILayout.Button("Next >", new GUILayoutOption[] { GUILayout.Width(80) }))
                {
                    CompPages.TurnPage(Turn.Right, ref this.m_compScroll);
                }
            }
            GUILayout.EndHorizontal();

            GUI.skin.button.alignment = TextAnchor.MiddleLeft;
            if (m_cachedDestroyList.Count > 0)
            {
                m_cachedDestroyList.Clear();
            }

            if (m_components != null)
            {
                int start = CompPages.CalculateOffsetIndex();

                for (int j = start; (j < start + CompPages.ItemsPerPage && j < CompPages.ItemCount); j++)
                {
                    var component = m_components[j];

                    if (!component) continue;

                    var ilType = component.GetIl2CppType();

                    GUILayout.BeginHorizontal(null);
                    if (ReflectionHelpers.BehaviourType.IsAssignableFrom(ilType))
                    {
                        BehaviourEnabledBtn(component.TryCast<Behaviour>());
                    }
                    else
                    {
                        GUILayout.Space(26);
                    }
                    if (GUILayout.Button("<color=cyan>" + ilType.Name + "</color>", new GUILayoutOption[] { GUILayout.Width(m_rect.width / 2 - 100) }))
                    {
                        ReflectObject(component);
                    }
                    if (GUILayout.Button("<color=red>-</color>", new GUILayoutOption[] { GUILayout.Width(20) }))
                    {
                        m_cachedDestroyList.Add(component);
                    }
                    GUILayout.EndHorizontal();
                }
            }

            GUI.skin.button.alignment = TextAnchor.MiddleCenter;
            if (m_cachedDestroyList.Count > 0)
            {
                for (int i = m_cachedDestroyList.Count - 1; i >= 0; i--)
                {
                    var comp = m_cachedDestroyList[i];
                    GameObject.Destroy(comp);
                }
            }

            GUIUnstrip.EndScrollView();

            GUILayout.EndVertical();
        }

        private void BehaviourEnabledBtn(Behaviour obj)
        {
            var _col = GUI.color;
            bool _enabled = obj.enabled;
            if (_enabled)
            {
                GUI.color = Color.green;
            }
            else
            {
                GUI.color = Color.red;
            }

            // ------ toggle active button ------

            _enabled = GUILayout.Toggle(_enabled, "", new GUILayoutOption[] { GUILayout.Width(18) });
            if (obj.enabled != _enabled)
            {
                obj.enabled = _enabled;
            }
            GUI.color = _col;
        }

        private void GameObjectControls()
        {
            GUILayout.BeginVertical(GUI.skin.box, new GUILayoutOption[] { GUILayout.Width(520) });
            GUILayout.Label("<b><size=15>GameObject Controls</size></b>", null);

            GUILayout.BeginHorizontal(null);
            bool m_active = m_object.activeSelf;
            m_active = GUILayout.Toggle(m_active, (m_active ? "<color=lime>Enabled " : "<color=red>Disabled") + "</color>",
                new GUILayoutOption[] { GUILayout.Width(80) });
            if (m_object.activeSelf != m_active) { m_object.SetActive(m_active); }

            UIHelpers.InstantiateButton(m_object, 100);

            if (GUILayout.Button("Set DontDestroyOnLoad", new GUILayoutOption[] { GUILayout.Width(170) }))
            {
                GameObject.DontDestroyOnLoad(m_object);
                m_object.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            }

            var lbl = m_freeze ? "<color=lime>Unfreeze</color>" : "<color=orange>Freeze Pos/Rot</color>";
            if (GUILayout.Button(lbl, new GUILayoutOption[] { GUILayout.Width(110) }))
            {
                m_freeze = !m_freeze;
                if (m_freeze)
                {
                    UpdateFreeze();
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(null);

            m_setParentInput = GUILayout.TextField(m_setParentInput, null);
            if (GUILayout.Button("Set Parent", new GUILayoutOption[] { GUILayout.Width(80) }))
            {
                if (GameObject.Find(m_setParentInput) is GameObject newparent)
                {
                    m_object.transform.parent = newparent.transform;
                }
                else
                {
                    MelonLogger.LogWarning($"Could not find gameobject '{m_setParentInput}'");
                }
            }

            if (GUILayout.Button("Detach from parent", new GUILayoutOption[] { GUILayout.Width(160) }))
            {
                m_object.transform.parent = null;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(GUI.skin.box, null);

            m_cachedInput[0] = TranslateControl(TranslateType.Position, ref m_translateAmount,  false);
            m_cachedInput[1] = TranslateControl(TranslateType.Rotation, ref m_rotateAmount,     true);
            m_cachedInput[2] = TranslateControl(TranslateType.Scale,    ref m_scaleAmount,      false);

            GUILayout.BeginHorizontal(null);
            if (GUILayout.Button("<color=lime>Apply to Transform</color>", null) || m_autoApplyTransform)
            {
                if (m_localContext)
                {
                    m_object.transform.localPosition = m_cachedInput[0];
                    m_object.transform.localEulerAngles = m_cachedInput[1];
                }
                else
                {
                    m_object.transform.position = m_cachedInput[0];
                    m_object.transform.eulerAngles = m_cachedInput[1];
                }
                m_object.transform.localScale = m_cachedInput[2];

                if (m_freeze)
                {
                    UpdateFreeze();
                }
            }
            if (GUILayout.Button("<color=lime>Update from Transform</color>", null) || m_autoUpdateTransform)
            {
                CacheTransformValues();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(null);
            BoolToggle(ref m_autoApplyTransform, "Auto-apply to Transform?");
            BoolToggle(ref m_autoUpdateTransform, "Auto-update from transform?");
            GUILayout.EndHorizontal();

            bool b = m_localContext;
            b = GUILayout.Toggle(b, "<color=" + (b ? "lime" : "red") + ">Use local transform values?</color>", null);
            if (b != m_localContext)
            {
                m_localContext = b;
                CacheTransformValues();
                if (m_freeze)
                {
                    UpdateFreeze();
                }
            }

            GUILayout.EndVertical();

            if (GUILayout.Button("<color=red><b>Destroy</b></color>", new GUILayoutOption[] { GUILayout.Width(120) }))
            {
                GameObject.Destroy(m_object);
                DestroyWindow();
                return;
            }

            GUILayout.EndVertical();
        }

        private void UpdateFreeze()
        {
            if (m_localContext)
            {
                m_frozenPosition = m_object.transform.localPosition;
                m_frozenRotation = m_object.transform.localRotation;
            }
            else
            {
                m_frozenPosition = m_object.transform.position;
                m_frozenRotation = m_object.transform.rotation;
            }
            m_frozenScale = m_object.transform.localScale;
        }

        private void BoolToggle(ref bool value, string message)
        {
            string lbl = "<color=";
            lbl += value ? "lime" : "red";
            lbl += $">{message}</color>";

            value = GUILayout.Toggle(value, lbl, null);
        }

        public enum TranslateType
        {
            Position,
            Rotation,
            Scale
        }

        private Vector3 TranslateControl(TranslateType mode, ref float amount, bool multByTime)
        {
            GUILayout.BeginHorizontal(null);
            GUILayout.Label($"<color=cyan><b>{(m_localContext ? "Local " : "")}{mode}</b></color>:", 
                new GUILayoutOption[] { GUILayout.Width(m_localContext ? 110 : 65) });

            var transform = m_object.transform;
            switch (mode)
            {
                case TranslateType.Position:
                    var pos = m_localContext ? transform.localPosition : transform.position;
                    GUILayout.Label(pos.ToString(), new GUILayoutOption[] { GUILayout.Width(250) });
                    break;
                case TranslateType.Rotation:
                    var rot = m_localContext ? transform.localEulerAngles : transform.eulerAngles;
                    GUILayout.Label(rot.ToString(), new GUILayoutOption[] { GUILayout.Width(250) });
                    break;
                case TranslateType.Scale:
                    GUILayout.Label(transform.localScale.ToString(), new GUILayoutOption[] { GUILayout.Width(250) });
                    break;
            }
            GUILayout.EndHorizontal();

            Vector3 input = m_cachedInput[(int)mode];

            GUILayout.BeginHorizontal(null);
            GUI.skin.label.alignment = TextAnchor.MiddleRight;

            GUILayout.Label("<color=cyan>X:</color>", new GUILayoutOption[] { GUILayout.Width(20) });
            PlusMinusFloat(ref input.x, amount, multByTime);

            GUILayout.Label("<color=cyan>Y:</color>", new GUILayoutOption[] { GUILayout.Width(20) });
            PlusMinusFloat(ref input.y, amount, multByTime);

            GUILayout.Label("<color=cyan>Z:</color>", new GUILayoutOption[] { GUILayout.Width(20) });
            PlusMinusFloat(ref input.z, amount, multByTime);            

            GUILayout.Label("+/-:", new GUILayoutOption[] { GUILayout.Width(30) });
            var amountInput = amount.ToString("F3");
            amountInput = GUILayout.TextField(amountInput, new GUILayoutOption[] { GUILayout.Width(60) });
            if (float.TryParse(amountInput, out float f))
            {
                amount = f;
            }

            GUI.skin.label.alignment = TextAnchor.UpperLeft;
            GUILayout.EndHorizontal();

            return input;
        }

        private void PlusMinusFloat(ref float f, float amount, bool multByTime)
        {
            string s = f.ToString("F3");
            s = GUILayout.TextField(s, new GUILayoutOption[] { GUILayout.Width(60) });
            if (float.TryParse(s, out float f2))
            {
                f = f2;
            }
            if (GUILayout.RepeatButton("-", new GUILayoutOption[] { GUILayout.Width(20) }))
            {
                f -= multByTime ? amount * Time.deltaTime : amount;
            }
            if (GUILayout.RepeatButton("+", new GUILayoutOption[] { GUILayout.Width(20) }))
            {
                f += multByTime ? amount * Time.deltaTime : amount;
            }
        }
    }
}
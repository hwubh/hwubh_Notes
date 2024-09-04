using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.Overlays;
using Object = System.Object;
using UnityEngine.UIElements;


/// <summary>

/// </summary>
internal class EditorGUIBridge
{
    private static Assembly UnityEditorAssembly = typeof(EditorWindow).Assembly;

    internal class EditorContainerWindow
    {
        private static Type s_ContainerWindowType;
        private static MethodInfo s_OnResizeMethodInfo;
        private Object m_ContainerWindowObject;

        private static Type UnderlyingType
        {
            get
            {
                if (s_ContainerWindowType != null)
                    return s_ContainerWindowType;
                s_ContainerWindowType = UnityEditorAssembly.GetType("UnityEditor.ContainerWindow");
                if (s_ContainerWindowType == null)
                    throw new Exception("Failed to locate ContainerWindow type");
                return s_ContainerWindowType;
            }
        }

        internal System.Object UnderlyingObject => m_ContainerWindowObject;

        internal static object CreateInstance()
        {
            return ScriptableObject.CreateInstance(UnderlyingType);
        }

        public static void SetRootView(object instance, object value)
        {
            PropertyInfo pInfo =
                UnderlyingType.GetProperty("rootView", BindingFlags.Instance | BindingFlags.Public);
            if (pInfo == null) return;
            pInfo.SetValue(instance, value);
        }

        public static void SetRootViewPosition(object instance, Rect value)
        {
            PropertyInfo pInfo =
                UnderlyingType.GetProperty("rootView", BindingFlags.Instance | BindingFlags.Public);
            PropertyInfo pInfoB =
                pInfo.PropertyType.GetProperty("position", BindingFlags.Instance | BindingFlags.Public);
            if (pInfo == null || pInfoB == null) return;
            pInfoB.SetValue(pInfo.GetValue(instance), value);
        }

        public static void Show(object instance, int showMode, bool loadPosition, bool displayImmediately,
            bool setFocus)
        {
            MethodInfo mInfo = UnderlyingType.GetMethod("Show", BindingFlags.Public | BindingFlags.Instance, null,
                new Type[]
                {
                        typeof(EditorWindow).Assembly.GetType("UnityEditor.ShowMode"), typeof(bool), typeof(bool),
                        typeof(bool)
                }, null);
            if (mInfo == null) return;
            mInfo.Invoke(instance, new object[] { showMode, loadPosition, displayImmediately, setFocus });
        }

        public static void DisplayAllViews(object instance)
        {
            MethodInfo mInfo = UnderlyingType.GetMethod("DisplayAllViews",
                BindingFlags.Public | BindingFlags.Instance, null,
                new Type[] { }, null);
            if (mInfo == null) return;
            mInfo.Invoke(instance, null);
        }

        private static MethodInfo OnResizeMethodInfo
        {
            get
            {
                if (s_OnResizeMethodInfo != null)
                    return s_OnResizeMethodInfo;
                s_OnResizeMethodInfo =
                    UnderlyingType.GetMethod("OnResize", BindingFlags.Public | BindingFlags.Instance);
                return s_OnResizeMethodInfo;
            }
        }


        public static void OnResize(object instance)
        {
            if (OnResizeMethodInfo == null) return;
            OnResizeMethodInfo.Invoke(instance, null);
        }
    }

    internal class EditorSplitView
    {
        private static Type s_SplitViewType;
        private Object m_ContainerWindowObject;

        private static Type UnderlyingType
        {
            get
            {
                if (s_SplitViewType != null)
                    return s_SplitViewType;
                s_SplitViewType = UnityEditorAssembly.GetType("UnityEditor.SplitView");
                if (s_SplitViewType == null)
                    throw new Exception("Failed to locate SplitView type");
                return s_SplitViewType;
            }
        }

        internal System.Object UnderlyingObject => m_ContainerWindowObject;

        internal static object CreateInstance()
        {
            return ScriptableObject.CreateInstance(UnderlyingType);
        }

        public static void AddChild(object instance, object view)
        {
            MethodInfo mInfo = UnderlyingType.GetMethod("AddChild", BindingFlags.Public | BindingFlags.Instance,
                null,
                new Type[] { typeof(EditorWindow).Assembly.GetType("UnityEditor.View") }, null);
            if (mInfo == null) return;
            mInfo.Invoke(instance, new object[] { view });
        }
    }

    internal class EditorDockArea
    {
        private static Type s_DockAreaType;
        private Object m_ContainerWindowObject;

        private static Type UnderlyingType
        {
            get
            {
                if (s_DockAreaType != null)
                    return s_DockAreaType;
                s_DockAreaType = UnityEditorAssembly.GetType("UnityEditor.DockArea");
                if (s_DockAreaType == null)
                    throw new Exception("Failed to locate DockArea type");
                return s_DockAreaType;
            }
        }

        internal System.Object UnderlyingObject => m_ContainerWindowObject;

        internal static object CreateInstance()
        {
            return ScriptableObject.CreateInstance(UnderlyingType);
        }

        /// <summary>
        /// Add Tab
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="window"></param>
        /// <param name="sendPaneEvents"></param>
        public static void AddTab(object instance, EditorWindow window, bool sendPaneEvents = true)
        {
            MethodInfo mInfo = UnderlyingType.GetMethod("AddTab", BindingFlags.Instance | BindingFlags.Public, null,
                new Type[] { typeof(EditorWindow), typeof(bool) }, null);
            if (mInfo == null) return;
            mInfo.Invoke(instance, new object[] { window, sendPaneEvents });
        }

        /// <summary>
        /// Set Tab Position
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="position"></param>
        public static void SetPosition(object instance, Rect position)
        {
            PropertyInfo pInfo =
                UnderlyingType.GetProperty("position", BindingFlags.Instance | BindingFlags.Public);
            if (pInfo == null) return;
            pInfo.SetValue(instance, position);
        }
    }

    internal class EditorMainView

    {
        private static Type s_MainViewType;
        private Object m_MainViewObject;

        private static Type UnderlyingType
        {
            get
            {
                if (s_MainViewType != null)
                    return s_MainViewType;
                s_MainViewType = UnityEditorAssembly.GetType("UnityEditor.MainView");
                if (s_MainViewType == null)
                    throw new Exception("Failed to locate ContainerWindow type");
                return s_MainViewType;
            }
        }

        internal System.Object UnderlyingObject => m_MainViewObject;

        internal static object CreateInstance()
        {
            return ScriptableObject.CreateInstance(UnderlyingType);
        }

        public static void SetUseTopView(object instance, object value)
        {
            PropertyInfo pInfo =
                UnderlyingType.GetProperty("useTopView", BindingFlags.Instance | BindingFlags.Public);
            if (pInfo != null)
                pInfo.SetValue(instance, value);
        }

        public static void SetUseBottomView(object instance, object value)
        {
            PropertyInfo pInfo =
                UnderlyingType.GetProperty("useBottomView", BindingFlags.Instance | BindingFlags.Public);
            if (pInfo != null)
                pInfo.SetValue(instance, value);
        }

        public static void SetTopViewHeight(object instance, object value)
        {
            PropertyInfo pInfo =
                UnderlyingType.GetProperty("topViewHeight", BindingFlags.Instance | BindingFlags.Public);
            if (pInfo != null)
                pInfo.SetValue(instance, value);
        }

        public static void SetBottomViewHeight(object instance, object value)
        {
            PropertyInfo pInfo =
                UnderlyingType.GetProperty("bottomViewHeight", BindingFlags.Instance | BindingFlags.Public);
            if (pInfo != null)
                pInfo.SetValue(instance, value);
        }

        public static void AddChild(object instance, object view)
        {
            MethodInfo mInfo = UnderlyingType.GetMethod("AddChild", BindingFlags.Instance | BindingFlags.Public,
                null,
                new Type[] { typeof(EditorWindow).Assembly.GetType("UnityEditor.View") }, null);
            if (mInfo == null) return;
            mInfo.Invoke(instance, new object[] { view });
        }
    }

    internal class EditorViewView
    {
        private static Type s_ViewType;
        private Object m_ViewObject;

        private static Type UnderlyingType
        {
            get
            {
                if (s_ViewType != null)
                    return s_ViewType;
                s_ViewType = UnityEditorAssembly.GetType("UnityEditor.View");
                if (s_ViewType == null)
                    throw new Exception("Failed to locate ContainerWindow type");
                return s_ViewType;
            }
        }

        internal System.Object UnderlyingObject => m_ViewObject;

        public static void SetPosition(object instance, Rect position)
        {
            PropertyInfo pInfo =
                UnderlyingType.GetProperty("position", BindingFlags.Instance | BindingFlags.Public);
            if (pInfo == null) return;
            pInfo.SetValue(instance, position);
        }
    }

    internal class EditorEditorWindow
    {
        private static Type s_EditorWindowType;
        private Object m_EditorWindowObject;

        private static Type UnderlyingType
        {
            get
            {
                if (s_EditorWindowType != null)
                    return s_EditorWindowType;
                s_EditorWindowType = UnityEditorAssembly.GetType("UnityEditor.EditorWindow");
                if (s_EditorWindowType == null)
                    throw new Exception("Failed to locate EditorWindow type");
                return s_EditorWindowType;
            }
        }

        internal System.Object UnderlyingObject => m_EditorWindowObject;

        public static void MakeParentsSettingsMatchMe(object instance)
        {
            MethodInfo mInfo = UnderlyingType.GetMethod("MakeParentsSettingsMatchMe",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (mInfo == null) return;
            mInfo.Invoke(instance, null);
        }

        public static void SetOverlayCanvas(object instance, object canvas)
        {
            PropertyInfo pInfo =
                UnderlyingType.GetProperty("m_OverlayCanvas", BindingFlags.Instance | BindingFlags.NonPublic);
            if (pInfo == null) return;
            pInfo.SetValue(instance, canvas);
        }
    }

    internal class EditorOverlayCanvas
    {
        private static Type s_OverlayCanvasType;
        private Object m_OverlayCanvasObject;

        private static Type UnderlyingType
        {
            get
            {
                if (s_OverlayCanvasType != null)
                    return s_OverlayCanvasType;
                s_OverlayCanvasType = UnityEditorAssembly.GetType("UnityEditor.Overlays.OverlayCanvas");
                if (s_OverlayCanvasType == null)
                    throw new Exception("Failed to locate OverlayCanvas type");
                return s_OverlayCanvasType;
            }
        }

        internal System.Object UnderlyingObject => m_OverlayCanvasObject;

        internal static object CreateInstance()
        {
            return ScriptableObject.CreateInstance(UnderlyingType);
        }

        public static object CreateOverlayCanvas()
        {
            ConstructorInfo ctor =
                UnderlyingType.GetConstructor(null);

            if (ctor == null) return null;

            return ctor.Invoke(null);
        }

    }

    internal class EditorGUIView
    {
        private static Type s_GUIViewType;
        private static Type s_IWindowModelType;
        private Object m_GUIViewObject;

        private static Type UnderlyingType
        {
            get
            {
                if (s_GUIViewType != null)
                    return s_GUIViewType;
                s_GUIViewType = UnityEditorAssembly.GetType("UnityEditor.GUIView");
                if (s_GUIViewType == null)
                    throw new Exception("Failed to locate ContainerWindow type");
                return s_GUIViewType;
            }
        }

        private static Type UnderlyingIWindowModelType
        {
            get
            {
                if (s_IWindowModelType != null)
                    return s_IWindowModelType;
                s_IWindowModelType = UnityEditorAssembly.GetType("UnityEditor.IWindowModel");
                if (s_IWindowModelType == null)
                    throw new Exception("Failed to locate IWindowModel type");
                return s_IWindowModelType;
            }
        }

        internal System.Object UnderlyingObject => m_GUIViewObject;

        internal static object CreateInstance()
        {
            return ScriptableObject.CreateInstance(UnderlyingType);
        }

        public static VisualElement GetWindowBackend_VisualTree(object instance)
        {
            PropertyInfo pInfo = UnderlyingType.GetProperty("windowBackend", BindingFlags.Instance | BindingFlags.NonPublic);
            PropertyInfo pInfoB = pInfo.PropertyType.GetProperty("visualTree", BindingFlags.Instance | BindingFlags.Public);
            if (pInfo == null || pInfoB == null) return null;
            return (VisualElement)pInfoB.GetValue(pInfo.GetValue(instance));
        }

        public static void SetonGUIHandler(object instance, Action action)
        {
            PropertyInfo fInfo = UnderlyingIWindowModelType.GetProperty("onGUIHandler");
            if (fInfo == null) return;
            fInfo.SetValue(instance, action);
        }
    }

    internal class EditorAppStatusBar
    {
        private static Type s_AppStatusBarType;
        private Object m_AppStatusBarObject;

        private static Type UnderlyingType
        {
            get
            {
                if (s_AppStatusBarType != null)
                    return s_AppStatusBarType;
                s_AppStatusBarType = UnityEditorAssembly.GetType("UnityEditor.AppStatusBar");
                if (s_AppStatusBarType == null)
                    throw new Exception("Failed to locate AppStatusBar type");
                return s_AppStatusBarType;
            }
        }

        internal System.Object UnderlyingObject => m_AppStatusBarObject;

        internal static object CreateInstance()
        {
            return ScriptableObject.CreateInstance(UnderlyingType);
        }
    }
}


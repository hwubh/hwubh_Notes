using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class ContainerWindow : MonoBehaviour
{
    private static object m_ContainerViewInstance;
    private static object m_SplitViewInstance;
    private static object m_DockAreaInstance;

    private static EditorWindow1 m_EditorWindow1;
    private static EditorWindow2 m_EditorWindow2;
    private static EditorWindow3 m_EditorWindow3;
    private static EditorWindow4 m_EditorWindow4;

    class EditorWindow1 : EditorWindow
    {
        internal void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Label("This is EditorWindow1");
            EditorGUILayout.EndVertical();
        }
    }

    class EditorWindow2 : EditorWindow
    {
        internal void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Label("This is EditorWindow2");
            EditorGUILayout.EndVertical();
        }
    }

    class EditorWindow3 : EditorWindow
    {
        internal void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Label("This is EditorWindow3");
            EditorGUILayout.EndVertical();
        }
    }

    class EditorWindow4 : EditorWindow
    {
        internal void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Label("This is EditorWindow4");
            EditorGUILayout.EndVertical();
        }
    }

    [MenuItem("Tools/log")]
    public static void Test()
    {
        m_ContainerViewInstance = EditorGUIBridge.EditorContainerWindow.CreateInstance();
        m_SplitViewInstance = EditorGUIBridge.EditorSplitView.CreateInstance();
        m_DockAreaInstance = EditorGUIBridge.EditorDockArea.CreateInstance();

        m_EditorWindow1 = ScriptableObject.CreateInstance<EditorWindow1>();
        m_EditorWindow1.titleContent = new GUIContent("m_EditorWindow1");
        m_EditorWindow2 = ScriptableObject.CreateInstance<EditorWindow2>();
        m_EditorWindow2.titleContent = new GUIContent("m_EditorWindow2");
        m_EditorWindow3 = ScriptableObject.CreateInstance<EditorWindow3>();
        m_EditorWindow3.titleContent = new GUIContent("m_EditorWindow3");
        m_EditorWindow4 = ScriptableObject.CreateInstance<EditorWindow4>();
        m_EditorWindow4.titleContent = new GUIContent("m_EditorWindow4");

        EditorGUIBridge.EditorDockArea.SetPosition(m_DockAreaInstance, new Rect(0, 0, 200, 800));

        EditorGUIBridge.EditorDockArea.AddTab(m_DockAreaInstance, m_EditorWindow1);
        EditorGUIBridge.EditorDockArea.AddTab(m_DockAreaInstance, m_EditorWindow2);
        EditorGUIBridge.EditorDockArea.AddTab(m_DockAreaInstance, m_EditorWindow3);
        EditorGUIBridge.EditorDockArea.AddTab(m_DockAreaInstance, m_EditorWindow4);

        EditorGUIBridge.EditorSplitView.AddChild(m_SplitViewInstance, m_DockAreaInstance);

        float width = 800;
        float height = 800;

        var topView = EditorGUIBridge.EditorGUIView.CreateInstance();
        var visualTree = EditorGUIBridge.EditorGUIView.GetWindowBackend_VisualTree(topView);
        var titleUIElements = GetTileViewFromUXML();
        if (titleUIElements == null)
            throw new InvalidOperationException("Unable to create TitleView from Uxml. Uxml must contain at least one child element.");
        visualTree.Add(titleUIElements);

        var bottomView = EditorGUIBridge.EditorGUIView.CreateInstance();
        Action testAction = () =>
        {
            GUI.Label(GUILayoutUtility.GetRect(GUIContent.none, new GUIStyle("AppToolbar"), GUILayout.ExpandWidth(true)),
                "This is Bottom Bar", new GUIStyle("AppToolbar"));
        };
        var visualTree2 = EditorGUIBridge.EditorGUIView.GetWindowBackend_VisualTree(bottomView);
        visualTree2.Add(new IMGUIContainer(testAction));

        var main = EditorGUIBridge.EditorMainView.CreateInstance();
        EditorGUIBridge.EditorMainView.SetUseTopView(main, true);
        EditorGUIBridge.EditorMainView.SetUseBottomView(main, true);
        EditorGUIBridge.EditorMainView.SetTopViewHeight(main, 30f);
        EditorGUIBridge.EditorMainView.SetBottomViewHeight(main, 30f);

        if (topView != null)
        {
            var rect = new Rect(0, 0, width, 30f);
            EditorGUIBridge.EditorViewView.SetPosition(topView, rect);
            EditorGUIBridge.EditorMainView.AddChild(main, topView);
        }

        var centerViewHeight = height - 45f;
        EditorGUIBridge.EditorViewView.SetPosition(m_SplitViewInstance, new Rect(0, 30f, width, centerViewHeight));
        EditorGUIBridge.EditorMainView.AddChild(main, m_SplitViewInstance);

        if (bottomView != null)
        {
            var rect = new Rect(0, height - 15f, width, 15f);
            EditorGUIBridge.EditorViewView.SetPosition(bottomView, rect);
            EditorGUIBridge.EditorMainView.AddChild(main, bottomView);
        }

        EditorGUIBridge.EditorContainerWindow.SetRootView(m_ContainerViewInstance, main);
        EditorGUIBridge.EditorContainerWindow.SetRootViewPosition(m_ContainerViewInstance, new Rect(0, 0, width, height));
        EditorGUIBridge.EditorContainerWindow.Show(m_ContainerViewInstance, 2, true, false, false);
        EditorGUIBridge.EditorContainerWindow.DisplayAllViews(m_ContainerViewInstance);
        EditorGUIBridge.EditorContainerWindow.OnResize(m_ContainerViewInstance);
    }

    public static VisualElement GetTileViewFromUXML()
    {
        // Load Uxml template from disk.
        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Editor/Title.uxml");
        if (uxml == null)
            return null;

#if UNITY_2020_3_OR_NEWER
        var template = uxml.Instantiate();
#else
                var template = uxml.CloneTree();
#endif

        // Retrieve first child from template container.
        VisualElement view = null;
        using (var enumerator = template.Children().GetEnumerator())
        {
            if (enumerator.MoveNext())
                view = enumerator.Current;
        }

        return view;
    }
}

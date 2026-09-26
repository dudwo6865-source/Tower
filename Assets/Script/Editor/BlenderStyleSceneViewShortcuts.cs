using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

/// <summary>
/// Blender 스타일 Scene 뷰 키패드 단축키.
/// Edit → Shortcuts → Tank/블렌더식 시점 에서 키 변경 가능.
/// </summary>
public static class BlenderStyleSceneViewShortcuts
{
    const float OrbitStepDegrees = 15f;
    const string ShortcutRoot = "Tank/블렌더식 시점";

    static SceneView ActiveView => SceneView.lastActiveSceneView;

    static void AlignView(Vector3 lookDirection, Vector3 up, bool orthographic = true)
    {
        var view = ActiveView;
        if (view == null)
            return;

        if (Selection.activeTransform != null)
            view.pivot = Selection.activeTransform.position;

        view.rotation = Quaternion.LookRotation(lookDirection, up);
        view.orthographic = orthographic;
        view.Repaint();
    }

    static void OrbitView(float pitch, float yaw)
    {
        var view = ActiveView;
        if (view == null)
            return;

        var rotation = view.rotation;
        rotation = Quaternion.AngleAxis(yaw, Vector3.up) * rotation;
        rotation = Quaternion.AngleAxis(pitch, rotation * Vector3.right) * rotation;
        view.rotation = rotation;
        view.Repaint();
    }

    static void FrameSceneContents(SceneView view)
    {
        var hasBounds = false;
        var bounds = new Bounds();

        foreach (var renderer in Object.FindObjectsOfType<Renderer>())
        {
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds)
            view.Frame(bounds, false);
    }

    [Shortcut(ShortcutRoot + "/위", typeof(SceneView), KeyCode.Keypad7)]
    static void TopView() => AlignView(Vector3.down, Vector3.forward);

    [Shortcut(ShortcutRoot + "/아래", typeof(SceneView), KeyCode.Keypad7, ShortcutModifiers.Action)]
    static void BottomView() => AlignView(Vector3.up, Vector3.back);

    [Shortcut(ShortcutRoot + "/앞", typeof(SceneView), KeyCode.Keypad1)]
    static void FrontView() => AlignView(Vector3.forward, Vector3.up);

    [Shortcut(ShortcutRoot + "/뒤", typeof(SceneView), KeyCode.Keypad1, ShortcutModifiers.Action)]
    static void BackView() => AlignView(Vector3.back, Vector3.up);

    [Shortcut(ShortcutRoot + "/오른쪽", typeof(SceneView), KeyCode.Keypad3)]
    static void RightView() => AlignView(Vector3.left, Vector3.up);

    [Shortcut(ShortcutRoot + "/왼쪽", typeof(SceneView), KeyCode.Keypad3, ShortcutModifiers.Action)]
    static void LeftView() => AlignView(Vector3.right, Vector3.up);

    [Shortcut(ShortcutRoot + "/직교 전환", typeof(SceneView), KeyCode.Keypad5)]
    static void ToggleOrthographic()
    {
        var view = ActiveView;
        if (view == null)
            return;

        view.orthographic = !view.orthographic;
        view.Repaint();
    }

    [Shortcut(ShortcutRoot + "/아래로 회전", typeof(SceneView), KeyCode.Keypad2)]
    static void OrbitDown() => OrbitView(-OrbitStepDegrees, 0f);

    [Shortcut(ShortcutRoot + "/위로 회전", typeof(SceneView), KeyCode.Keypad8)]
    static void OrbitUp() => OrbitView(OrbitStepDegrees, 0f);

    [Shortcut(ShortcutRoot + "/왼쪽으로 회전", typeof(SceneView), KeyCode.Keypad4)]
    static void OrbitLeft() => OrbitView(0f, OrbitStepDegrees);

    [Shortcut(ShortcutRoot + "/오른쪽으로 회전", typeof(SceneView), KeyCode.Keypad6)]
    static void OrbitRight() => OrbitView(0f, -OrbitStepDegrees);

    [Shortcut(ShortcutRoot + "/선택 항목에 맞추기", typeof(SceneView), KeyCode.KeypadPeriod)]
    static void FrameSelected()
    {
        var view = ActiveView;
        if (view == null)
            return;

        if (Selection.activeTransform != null)
            view.FrameSelected();
        else
            FrameSceneContents(view);

        view.Repaint();
    }

    [Shortcut(ShortcutRoot + "/전체 보기", typeof(SceneView), KeyCode.KeypadPeriod, ShortcutModifiers.Shift)]
    static void FrameAllShortcut()
    {
        var view = ActiveView;
        if (view == null)
            return;

        FrameSceneContents(view);
        view.Repaint();
    }

    [Shortcut(ShortcutRoot + "/카메라 시점", typeof(SceneView), KeyCode.Keypad0)]
    static void ActiveCameraView()
    {
        var view = ActiveView;
        if (view == null)
            return;

        Camera cam = null;
        if (Selection.activeGameObject != null)
            cam = Selection.activeGameObject.GetComponent<Camera>();

        if (cam == null)
            cam = Camera.main;

        if (cam == null)
            cam = Object.FindObjectOfType<Camera>();

        if (cam == null)
            return;

        view.AlignViewToObject(cam.transform);
        view.Repaint();
    }
}

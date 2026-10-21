using System;
using UnityEditor;
using UnityEngine;

namespace PBTruong.BuildAnalyzer.Editor
{
    internal sealed class SnapshotLabelPopup : PopupWindowContent
    {
        private const string FieldName = "ba-snapshot-label";

        private readonly Action<string> _onSave;
        private string _label;
        private bool _focused;

        public SnapshotLabelPopup(string label, Action<string> onSave)
        {
            _label = label;
            _onSave = onSave;
        }

        public override Vector2 GetWindowSize() => new(320, 66);

        public override void OnGUI(Rect rect)
        {
            var submitted = Event.current.type == EventType.KeyDown
                            && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);

            GUILayout.Label("Snapshot Label", EditorStyles.boldLabel);
            GUI.SetNextControlName(FieldName);
            _label = EditorGUILayout.TextField(_label);
            if (!_focused)
            {
                EditorGUI.FocusTextInControl(FieldName);
                _focused = true;
            }

            if (!GUILayout.Button("Save Snapshot") && !submitted)
                return;

            _onSave(_label);
            editorWindow.Close();
            GUIUtility.ExitGUI();
        }
    }
}

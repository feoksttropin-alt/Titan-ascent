using UnityEngine;
using UnityEngine.EventSystems;

namespace TitanAscent.UI
{
    /// <summary>
    /// Reads gamepad UI navigation events from InputHandler and routes them into
    /// Unity's EventSystem so menus respond to the D-pad and face buttons
    /// without any per-menu gamepad code.
    ///
    /// Attach to the same GameObject as the EventSystem, or any persistent scene object.
    /// </summary>
    [DefaultExecutionOrder(-40)]   // after InputHandler (-50), before most UI scripts
    public class GamepadUINavigator : MonoBehaviour
    {
        [Header("Repeat Settings")]
        [Tooltip("Seconds before held direction starts repeating.")]
        [SerializeField] private float repeatDelay    = 0.4f;
        [Tooltip("Seconds between repeated navigations while direction is held.")]
        [SerializeField] private float repeatInterval = 0.12f;

        private TitanAscent.Input.InputHandler _ih;

        // Repeat tracking per axis
        private float _upHeldTime    = 0f;
        private float _downHeldTime  = 0f;
        private float _leftHeldTime  = 0f;
        private float _rightHeldTime = 0f;
        private float _upRepeatTimer    = 0f;
        private float _downRepeatTimer  = 0f;
        private float _leftRepeatTimer  = 0f;
        private float _rightRepeatTimer = 0f;

        private void Awake()
        {
            _ih = TitanAscent.Input.InputHandler.Instance;
        }

        private void Update()
        {
            if (_ih == null)
            {
                _ih = TitanAscent.Input.InputHandler.Instance;
                if (_ih == null) return;
            }

            if (!_ih.IsGamepadActive) return;

            HandleDirection(_ih.UINavigateUp,    ref _upHeldTime,    ref _upRepeatTimer,    MoveDirection.Up);
            HandleDirection(_ih.UINavigateDown,  ref _downHeldTime,  ref _downRepeatTimer,  MoveDirection.Down);
            HandleDirection(_ih.UINavigateLeft,  ref _leftHeldTime,  ref _leftRepeatTimer,  MoveDirection.Left);
            HandleDirection(_ih.UINavigateRight, ref _rightHeldTime, ref _rightRepeatTimer, MoveDirection.Right);

            if (_ih.UIConfirm)  SendSubmit();
            if (_ih.UICancel)   SendCancel();
        }

        private void HandleDirection(bool pressed, ref float heldTime, ref float repeatTimer, MoveDirection dir)
        {
            if (pressed)
            {
                // One-shot on first press
                heldTime    = Time.deltaTime;
                repeatTimer = repeatDelay;
                Navigate(dir);
            }
            else if (heldTime > 0f)
            {
                // Reset when released
                heldTime    = 0f;
                repeatTimer = repeatDelay;
            }
        }

        private static void Navigate(MoveDirection dir)
        {
            if (EventSystem.current == null) return;

            AxisEventData data = new AxisEventData(EventSystem.current)
            {
                moveDir = dir
            };
            ExecuteEvents.Execute(EventSystem.current.currentSelectedGameObject,
                                  data,
                                  ExecuteEvents.moveHandler);
        }

        private static void SendSubmit()
        {
            if (EventSystem.current == null) return;

            BaseEventData data = new BaseEventData(EventSystem.current);
            ExecuteEvents.Execute(EventSystem.current.currentSelectedGameObject,
                                  data,
                                  ExecuteEvents.submitHandler);
        }

        private static void SendCancel()
        {
            if (EventSystem.current == null) return;

            BaseEventData data = new BaseEventData(EventSystem.current);
            ExecuteEvents.Execute(EventSystem.current.currentSelectedGameObject,
                                  data,
                                  ExecuteEvents.cancelHandler);
        }
    }
}

using RealMolkky.Molkky;
using UnityEngine;

namespace RealMolkky.Play
{
    /// <summary>
    /// 投げる人の背後から見るカメラ。立ち位置に合わせてなめらかに動き、ピンの並びの方を向く。
    /// </summary>
    public class ThrowCamera : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [SerializeField] float smoothTime = 0.15f;

        public float Lateral { get; set; }
        public ThrowControlTuning Tuning { get; set; }

        Vector3 _vel;
        Vector3 _lookVel;
        Vector3 _look;
        bool _initialized;

        public Camera Camera => cam;

        public void Bind(Camera c) => cam = c;

        void Reset() => cam = GetComponent<Camera>();

        void Desired(out Vector3 position, out Vector3 lookAt)
        {
            var t = Tuning ?? new ThrowControlTuning();
            position = new Vector3(Lateral, t.cameraHeight, -t.cameraBack);
            var target = SwipeThrowMapper.PinsCenter;
            lookAt = new Vector3(t.faceTarget ? target.x : Lateral, 0.05f, target.z);
        }

        public void SnapToTarget()
        {
            Desired(out var pos, out _look);
            cam.transform.position = pos;
            cam.transform.LookAt(_look);
            _vel = Vector3.zero;
            _lookVel = Vector3.zero;
            _initialized = true;
        }

        void LateUpdate()
        {
            if (cam == null) return;
            if (Tuning != null) cam.fieldOfView = Tuning.cameraFov;
            if (!_initialized)
            {
                SnapToTarget();
                return;
            }

            Desired(out var pos, out var look);
            var tr = cam.transform;
            tr.position = Vector3.SmoothDamp(tr.position, pos, ref _vel, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            _look = Vector3.SmoothDamp(_look, look, ref _lookVel, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            tr.LookAt(_look);
        }
    }
}

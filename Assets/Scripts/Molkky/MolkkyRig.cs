using UnityEngine;

namespace RealMolkky.Molkky
{
    /// <summary>
    /// 棒とピン12本の剛体をまとめて持つ。物理の設定・並べ直し・姿勢の読み書きを担当する。
    /// 並び: bodies[0] = 棒、bodies[1..12] = ピン番号。
    /// </summary>
    public class MolkkyRig : MonoBehaviour
    {
        // 調整値はシーンに保存しない（保存すると古い値がコードの初期値より優先され、直したはずの値が効かなくなる）。
        // 実行中は画面の Tuning から変える
        [System.NonSerialized] public PhysicsTuning tuning = new PhysicsTuning();

        [SerializeField] Rigidbody stick;
        [SerializeField] Rigidbody[] pins = new Rigidbody[MolkkySpec.PinCount]; // [0] = 1番
        [SerializeField] Collider ground;

        public Rigidbody Stick => stick;
        public Collider Ground => ground;

        readonly Rigidbody[] _bodies = new Rigidbody[MolkkySpec.BodyCount];
        Collider[] _colliders;
        PhysicsMaterial _woodMat;
        PhysicsMaterial _groundMat;

        public Rigidbody GetBody(int index) => _bodies[index];

        /// <summary>シーン生成時に参照を渡す</summary>
        public void Bind(Rigidbody stickBody, Rigidbody[] pinBodies, Collider groundCollider)
        {
            stick = stickBody;
            pins = pinBodies;
            ground = groundCollider;
        }

        void Awake() => Initialize();

        /// <summary>参照をまとめて物理を設定し、並べ直す（エディタのテストからも呼ぶ）</summary>
        public void Initialize()
        {
            if (_colliders != null) return;
            _bodies[MolkkySpec.StickBodyIndex] = stick;
            for (int i = 0; i < MolkkySpec.PinCount; i++) _bodies[i + 1] = pins[i];
            _colliders = new Collider[MolkkySpec.BodyCount];
            for (int i = 0; i < _bodies.Length; i++) _colliders[i] = _bodies[i].GetComponent<Collider>();
            ApplyTuning();
            ResetPins();
        }

        /// <summary>調整値を物理エンジンと剛体に反映する</summary>
        public void ApplyTuning()
        {
            var t = tuning;
            Time.fixedDeltaTime = t.fixedDeltaTime;
            Physics.defaultSolverIterations = t.solverIterations;
            Physics.defaultSolverVelocityIterations = t.solverVelocityIterations;
            Physics.defaultContactOffset = t.contactOffset;
            Physics.bounceThreshold = t.bounceThreshold;
            Physics.sleepThreshold = t.sleepThreshold;

            _woodMat ??= new PhysicsMaterial("Wood");
            _woodMat.staticFriction = t.woodStaticFriction;
            _woodMat.dynamicFriction = t.woodDynamicFriction;
            _woodMat.bounciness = t.woodBounciness;
            _woodMat.frictionCombine = PhysicsMaterialCombine.Average;
            _woodMat.bounceCombine = PhysicsMaterialCombine.Average;

            _groundMat ??= new PhysicsMaterial("Grass");
            _groundMat.staticFriction = t.groundStaticFriction;
            _groundMat.dynamicFriction = t.groundDynamicFriction;
            _groundMat.bounciness = t.groundBounciness;
            _groundMat.frictionCombine = PhysicsMaterialCombine.Average;
            _groundMat.bounceCombine = PhysicsMaterialCombine.Average;

            ground.sharedMaterial = _groundMat;
            ground.contactOffset = t.contactOffset;

            for (int i = 0; i < _bodies.Length; i++)
            {
                var rb = _bodies[i];
                var col = _colliders[i];
                col.sharedMaterial = _woodMat;
                col.contactOffset = t.contactOffset;

                rb.solverIterations = t.solverIterations;
                rb.solverVelocityIterations = t.solverVelocityIterations;
                rb.sleepThreshold = t.sleepThreshold;
                rb.maxAngularVelocity = t.maxAngularVelocity;
                rb.linearDamping = t.linearDamping;
                rb.angularDamping = t.angularDamping;
                rb.interpolation = RigidbodyInterpolation.None; // 自前で毎フレーム進めるため不要
                if (!rb.isKinematic) rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // すり抜け対策

                // 重心と慣性は当たり判定の形から自動で求め、質量だけ体積×密度にする
                rb.ResetCenterOfMass();
                rb.ResetInertiaTensor();
                rb.mass = Volume(i) * t.woodDensity;
            }
        }

        static float Volume(int bodyIndex)
        {
            float area = Mathf.PI * MolkkySpec.PinRadius * MolkkySpec.PinRadius;
            if (bodyIndex == MolkkySpec.StickBodyIndex) return area * MolkkySpec.StickLength;
            return area * (MolkkySpec.PinHeight + MolkkySpec.PinCutLowHeight) * 0.5f;
        }

        /// <summary>ピンを最初の並びに戻し、棒を手元に置く</summary>
        public void ResetPins()
        {
            for (int n = 1; n <= MolkkySpec.PinCount; n++)
            {
                SetDynamicPose(_bodies[n], MolkkySpec.InitialPinPosition(n), Quaternion.identity);
            }
            HoldStick(new Vector3(0f, 0.6f, 0.2f), Quaternion.identity);
            Physics.SyncTransforms();
            for (int n = 1; n <= MolkkySpec.PinCount; n++) _bodies[n].Sleep();
        }

        /// <summary>投げる前の棒を空中に固定する</summary>
        public void HoldStick(Vector3 position, Quaternion rotation)
        {
            SetKinematic(stick, true);
            stick.position = position;
            stick.rotation = rotation;
            stick.transform.SetPositionAndRotation(position, rotation);
        }

        public void ReleaseStick(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
        {
            SetDynamicPose(stick, position, rotation);
            stick.linearVelocity = velocity;
            stick.angularVelocity = angularVelocity;
            Physics.SyncTransforms();
            stick.WakeUp();
        }

        static void SetDynamicPose(Rigidbody rb, Vector3 position, Quaternion rotation)
        {
            SetKinematic(rb, false);
            rb.position = position;
            rb.rotation = rotation;
            rb.transform.SetPositionAndRotation(position, rotation);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        /// <summary>記録の再生用: 全部を物理から外して姿勢を直接置けるようにする</summary>
        public void SetAllKinematic(bool kinematic)
        {
            foreach (var rb in _bodies) SetKinematic(rb, kinematic);
        }

        /// <summary>キネマティックは連続衝突判定のうち Speculative しか使えないので切り替えに合わせる</summary>
        static void SetKinematic(Rigidbody rb, bool kinematic)
        {
            if (kinematic)
            {
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.isKinematic = true;
            }
            else
            {
                rb.isKinematic = false;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        public void SetPoseDirect(int index, Vector3 position, Quaternion rotation)
        {
            _bodies[index].transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>物理の1ステップの前に呼ぶ: 芝の転がり抵抗と、場外に落ちた物の停止</summary>
        public void PreStep(float dt)
        {
            float groundTop = ground.bounds.max.y;
            float k = Mathf.Clamp01(tuning.groundRollingResistance * dt);
            for (int i = 0; i < _bodies.Length; i++)
            {
                var rb = _bodies[i];
                if (rb.isKinematic || rb.IsSleeping()) continue;

                if (rb.position.y < groundTop - 2f)
                {
                    SetKinematic(rb, true); // 場外に落ちた
                    continue;
                }
                if (_colliders[i].bounds.min.y <= groundTop + 0.004f)
                {
                    rb.angularVelocity *= 1f - k;
                }
            }
        }

        /// <summary>動いている物の中で一番速い値（止まった判定用）</summary>
        public void MaxSpeeds(out float linear, out float angular)
        {
            linear = 0f;
            angular = 0f;
            foreach (var rb in _bodies)
            {
                if (rb.isKinematic || rb.IsSleeping()) continue;
                linear = Mathf.Max(linear, rb.linearVelocity.magnitude);
                angular = Mathf.Max(angular, rb.angularVelocity.magnitude);
            }
        }

        public void WritePoses(ThrowRecord record, int frame)
        {
            for (int i = 0; i < _bodies.Length; i++)
            {
                var tr = _bodies[i].transform;
                record.SetPose(frame, i, tr.position, tr.rotation);
            }
        }
    }
}

using UnityEngine;

namespace RealMolkky.Molkky
{
    /// <summary>
    /// 投げの記録を再生する（物理計算はしない）。オンラインで受け取った側もこれを使う。
    /// </summary>
    public class ThrowPlayer : MonoBehaviour
    {
        [SerializeField] MolkkyRig rig;

        public bool IsPlaying { get; private set; }
        public float PlayTime { get; private set; }
        public float Speed { get; set; } = 1f;
        public ThrowRecord Record { get; private set; }

        public void Bind(MolkkyRig r) => rig = r;

        public void Play(ThrowRecord record)
        {
            if (record == null || record.FrameCount == 0) return;
            Record = record;
            PlayTime = 0f;
            IsPlaying = true;
            rig.SetAllKinematic(true);
            Apply(0f);
        }

        public void Stop()
        {
            IsPlaying = false;
        }

        void Update()
        {
            if (!IsPlaying) return;
            PlayTime += Time.deltaTime * Speed;
            if (PlayTime >= Record.Duration)
            {
                PlayTime = Record.Duration;
                IsPlaying = false;
            }
            Apply(PlayTime);
        }

        void Apply(float time)
        {
            for (int i = 0; i < Record.BodyCount; i++)
            {
                Record.Sample(time, i, out var pos, out var rot);
                rig.SetPoseDirect(i, pos, rot);
            }
        }
    }
}

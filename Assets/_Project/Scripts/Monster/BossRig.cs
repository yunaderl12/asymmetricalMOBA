using System.Collections.Generic;
using UnityEngine;

namespace Game.Monster
{
    /// クリップを持たないボスのモデルの骨を、コードで回す。
    /// 毎フレーム Begin() で全部の骨を元の姿勢に戻し、親から順に Rotate() を重ねる。
    /// 回転はモデルの向き（+Z が前、+Y が上、+X が右）で指定する。骨ごとの向きの違いは気にしなくてよい。
    public class BossRig
    {
        private struct Bone
        {
            public Transform transform;
            public Quaternion restLocal;
        }

        private readonly Transform root;
        private readonly Dictionary<string, Bone> bones = new Dictionary<string, Bone>();
        private readonly Quaternion rootRest; // モデルの根元の回転（モデルの向きの基準）

        public BossRig(Transform modelRoot)
        {
            root = modelRoot;
            rootRest = Quaternion.identity;
            foreach (var t in modelRoot.GetComponentsInChildren<Transform>(true))
                if (!bones.ContainsKey(t.name)) bones[t.name] = new Bone { transform = t, restLocal = t.localRotation };
        }

        public bool Has(string name) => bones.ContainsKey(name);

        public Transform Get(string name) => bones.TryGetValue(name, out var b) ? b.transform : null;

        public void Begin()
        {
            foreach (var b in bones.Values)
                if (b.transform != root) b.transform.localRotation = b.restLocal;
        }

        /// 骨をその付け根まわりに回す。r はモデルの向きでの回転。親を先に回すと、子はそれに乗った上で回る。
        public void Rotate(string name, Quaternion r)
        {
            if (!bones.TryGetValue(name, out var b) || b.transform.parent == null) return;
            Quaternion frame = root.rotation * rootRest;
            Quaternion world = frame * r * Quaternion.Inverse(frame) * b.transform.rotation;
            b.transform.localRotation = Quaternion.Inverse(b.transform.parent.rotation) * world;
        }

        public void Rotate(string name, Vector3 axis, float degrees)
        {
            if (Mathf.Abs(degrees) > 0.001f) Rotate(name, Quaternion.AngleAxis(degrees, axis));
        }
    }
}

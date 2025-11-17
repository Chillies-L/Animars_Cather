using UnityEngine;
using System.Collections.Generic;
using Random = UnityEngine.Random;

namespace AnimarsCatcher
{
    // ✅ 把接口定义加回去
    public interface ICanShoot
    {
        bool CheckCanShoot(Vector3 position);
        bool HasDestroyed();
    }

    public class FragileItem : MonoBehaviour, ICanShoot, IResource
    {
        [SerializeField] private int mResourceCount;
        public int ResourceCount => mResourceCount;

        [Header("生命值设置")]
        public int MaxHP = 100;
        public ReactiveProperty<int> HP;

        [Header("掉落物列表")]
        public List<GameObject> PickableCrystal;

        private LayerMask mMask;
        private int mSelfLayerMask;
        private HPBar mHpBar;

        private void Awake()
        {
            HP = new ReactiveProperty<int>(MaxHP);

            mMask = (1 << LayerMask.NameToLayer("Ani")) | (1 << LayerMask.NameToLayer("Player"));
            mMask = ~mMask;
            mSelfLayerMask = gameObject.layer;

            var hpTransform = transform.Find("HPCanvas/HPBarBg/HPBar");
            if (hpTransform != null)
            {
                mHpBar = hpTransform.GetComponent<HPBar>();
                if (mHpBar != null)
                    mHpBar.Init(HP);
                else
                    Debug.LogWarning("[FragileItem] 找不到 HPBar 组件", this);
            }
            else
            {
                Debug.LogWarning("[FragileItem] 路径 HPCanvas/HPBarBg/HPBar 不存在", this);
            }
        }

        private void Start()
        {
            HP.Subscribe(hp =>
            {
                if (hp <= 0)
                {
                    for (int i = 0; i < mResourceCount; i++)
                    {
                        Vector3 pos = new Vector3(
                            transform.position.x + Random.Range(-3f, 3f),
                            transform.position.y,
                            transform.position.z + Random.Range(-3f, 3f));
                        var prefab = PickableCrystal[Random.Range(0, PickableCrystal.Count)];
                        var go = Instantiate(prefab, pos, Quaternion.identity);
                        go.transform.localScale = Vector3.one * 3;
                    }

                    Destroy(gameObject);
                }
            });
        }

        public bool CheckCanShoot(Vector3 position)
        {
            Vector3 dir = transform.position - position;
            Physics.Raycast(position, dir, out var hitInfo, 30, mMask);
            if (hitInfo.transform != null)
                return hitInfo.transform.CompareTag("FragileItem");
            return false;
        }

        public bool HasDestroyed()
        {
            return HP != null && HP.Value <= 0;
        }

        private void OnMouseEnter()
        {
            gameObject.layer = LayerMask.NameToLayer("SelectedObject");
        }

        private void OnMouseExit()
        {
            gameObject.layer = mSelfLayerMask;
        }
    }
}

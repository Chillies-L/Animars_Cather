using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace AnimarsCatcher
{
    public interface ICanPick
    {
        bool CheckCanPick();
        bool CheckCanCarry();
    }

    public enum PickableItemType
    {
        Food,
        Crystal,
        BluePrint
    }

    public enum FoodType
    {
        Simple,
        AddSpeed
    }

    public class PickableItem : MonoBehaviour, ICanPick, IResource
    {
        [SerializeField] private int mResourceCount;
        public int ResourceCount => mResourceCount;

        [Header("物品类型")]
        public PickableItemType ItemType;
        public FoodType FoodType;

        [Header("可拾取点位（自动生成）")]
        public List<Vector3> Positions = new List<Vector3>();

        [Header("同时可参与搬运的最大数量")]
        public int MaxAniCount = 2;
        public ReactiveProperty<int> CurrentAniCount = new(0);

        private List<PICKER_Ani> mAnis;
        private NavMeshAgent mTeamAgent;
        private Transform mHomeTransform;
        private Transform mPickedTrans;

        private LayerMask mLayerMask;
        private bool mIsPicked = false;
        private AudioSource mPickedAudioSource;

        private TextMeshProUGUI mText_CurrentAniCount;

        private void Awake()
        {
            mAnis = new List<PICKER_Ani>();

            // ✅ 自动生成可拾取位置（环绕一圈）
            GeneratePositions();

            // 初始化组件
            mTeamAgent = GetComponent<NavMeshAgent>();
            mTeamAgent.enabled = false;
            mHomeTransform = GameObject.FindWithTag("Home").transform;
            mPickedTrans = mHomeTransform.Find("PickedPos").transform;

            mLayerMask = gameObject.layer;
            mPickedAudioSource = GetComponent<AudioSource>();

            // 初始化 UI
            transform.Find("PickerCanvas/MaxAniCount").GetComponent<TextMeshProUGUI>().text = MaxAniCount.ToString();
            mText_CurrentAniCount = transform.Find("PickerCanvas/CurrentAniCount").GetComponent<TextMeshProUGUI>();
            mText_CurrentAniCount.text = CurrentAniCount.Value.ToString();
            CurrentAniCount.Subscribe(count =>
            {
                mText_CurrentAniCount.text = count.ToString();
            });
        }

        private void Update()
        {
            TeamAgentMove();
        }

        /// <summary>
        /// 自动生成拾取点位（根据 MaxAniCount 环绕生成）
        /// </summary>
        private void GeneratePositions(float radius = 1.5f)
        {
            Positions.Clear();
            for (int i = 0; i < MaxAniCount; i++)
            {
                float angle = i * Mathf.PI * 2 / MaxAniCount;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                Positions.Add(offset);
            }
        }

        /// <summary>
        /// 让 NavMeshAgent 驱动资源回家
        /// </summary>
        private void TeamAgentMove()
        {
            if (CheckCanCarry() && !mTeamAgent.enabled)
            {
                mTeamAgent.enabled = true;
            }

            if (mTeamAgent.enabled)
            {
                mTeamAgent.SetDestination(mHomeTransform.position);
                mTeamAgent.speed = (float)CurrentAniCount.Value / MaxAniCount * 2 * Const.CarrySpeed;
            }

            // 到家处理
            if (Vector3.Distance(transform.GetPositionOnTerrain(), mHomeTransform.position)
                < mTeamAgent.stoppingDistance && !mIsPicked)
            {
                mIsPicked = true;
                foreach (var ani in mAnis)
                {
                    ani.ReadyToCarry = false;
                    ani.IsPick = false;
                }
                mAnis.Clear();
                Positions.Clear();

                // ✅ 添加资源
                switch (ItemType)
                {
                    case PickableItemType.Food:
                        FindObjectOfType<GameRoot>().GameModel.FoodSum.Value += mResourceCount;
                        if (FoodType == FoodType.AddSpeed)
                            AddSpeedFood();
                        break;

                    case PickableItemType.Crystal:
                        FindObjectOfType<GameRoot>().GameModel.CrystalSum.Value += mResourceCount;
                        break;
                }

                if (!mPickedAudioSource.isPlaying) mPickedAudioSource.Play();
                transform.DOMove(mPickedTrans.position, 2f);
                transform.DOScale(Vector3.zero, 2f).OnComplete(() =>
                {
                    Destroy(gameObject);
                });
            }
        }

        /// <summary>
        /// 加速食物效果
        /// </summary>
        private void AddSpeedFood()
        {
            var player = FindObjectOfType<Player>();
            if (player == null) return;
            Debug.Log("Add Speed");
            player.SetAnisMoveSpeed(Const.FastMoveSpeed);
            player.SetAnimsCarrySpeed(Const.FastCarrySpeed);

            TimerManager.Instance.AddTask(() =>
            {
                Debug.Log("Add Speed End");
                player.SetAnisMoveSpeed(Const.BaseMoveSpeed);
                player.SetAnimsCarrySpeed(Const.BaseCarrySpeed);
            }, 10, 1);
        }

        /// <summary>
        /// 获取指定拾取者的拾取位置
        /// </summary>
        public Vector3 GetPosition(PICKER_Ani ani)
        {
            int index = mAnis.IndexOf(ani);

            // ✅ 防御性检查
            if (index < 0 || index >= Positions.Count)
            {
                Debug.LogWarning($"[PickableItem] GetPosition 越界，ani:{ani?.name ?? "null"} index:{index} Positions.Count:{Positions.Count}");
                return transform.position;
            }

            return transform.TransformPoint(Positions[index]);
        }

        public bool CheckCanPick()
        {
            return CurrentAniCount.Value < MaxAniCount;
        }

        public bool CheckCanCarry()
        {
            if (CurrentAniCount.Value > 0 && CurrentAniCount.Value >= MaxAniCount / 2)
            {
                foreach (var ani in mAnis)
                {
                    if (!ani.ReadyToCarry) return false;
                }
                return true;
            }
            return false;
        }

        public void AddPickerAni(PICKER_Ani pickerAni)
        {
            if (!mAnis.Contains(pickerAni))
            {
                mAnis.Add(pickerAni);
                CurrentAniCount.Value++;
            }
        }

        private void OnMouseEnter()
        {
            gameObject.layer = LayerMask.NameToLayer("SelectedObject");
        }

        private void OnMouseExit()
        {
            gameObject.layer = mLayerMask;
        }

#if UNITY_EDITOR
        // ✅ 在 Scene 视图中绘制拾取点（方便调试）
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            foreach (var pos in Positions)
            {
                Gizmos.DrawSphere(transform.TransformPoint(pos), 0.15f);
            }
        }
#endif
    }
}

using UnityEngine.UI;

namespace Platformer.UI
{
    using UnityEngine;
    using TMPro;
    using System.Text;

    public class HUD : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI m_progressTimeText;
        [SerializeField] private TextMeshProUGUI m_doorKeyCountText;
        [SerializeField] private TextMeshProUGUI m_stageNumberText;
        [SerializeField] private RectTransform m_rightSideBarRectTransform;
        [SerializeField] private GameObject m_pauseButton;
        [SerializeField] private GameObject m_pauseMenuPrefab;
        [SerializeField] private GameObject m_life;
        [SerializeField] private GameObject[] m_lives;
        [SerializeField] private bool m_showPauseButton;
        [SerializeField] private bool m_showLife;
        [SerializeField] private bool m_showHeader;
        [SerializeField] private GameObject m_portrait;
        [SerializeField] private GameObject m_landscape;
        [SerializeField] private Transform m_portraitLifeRoot;
        [SerializeField] private Transform m_portraitHeaderRoot;
        [SerializeField] private Transform m_portraitDummyMap;
        [SerializeField] private Transform m_portraitPauseMenuButtonRoot;
        [SerializeField] private Transform m_landscapeRightSideBarRoot;
        [SerializeField] private Transform m_landscapeHeaderRoot;
        [SerializeField] private Transform m_landscapeDummyMap;
        [SerializeField] private Transform[] m_headerTransforms;
        [SerializeField] private Transform m_portraitHardcoreProgressRoot;
        [SerializeField] private Transform m_landscapeHardcoreProgressRoot;
        [SerializeField] private Slider m_hardcoreSlider;
        [SerializeField] private TextMeshProUGUI m_hardcoreProgressText;
        [SerializeField] private Transform[] m_hardcoreProgressTranforms;

        private StringBuilder m_stringBuilder = new StringBuilder(32);
        private int m_doorKeyCount;
        private int m_maxDoorKeyCount;
        private RectTransform m_dummyMapRectTransform;

        public void UpdateUIByScreenOrientationChange(bool isLandscape)
        {
            ShowUI();

            m_portrait.SetActive(!isLandscape);
            m_landscape.SetActive(isLandscape);

            if (isLandscape)
            {
                m_pauseButton.transform.SetParent(m_landscapeRightSideBarRoot);
                m_life.transform.SetParent(m_landscapeRightSideBarRoot);
                m_landscapeHeaderRoot.SetAsLastSibling();

                foreach (var t in m_headerTransforms)
                {
                    t.SetParent(m_landscapeHeaderRoot);
                }

                m_landscapeHardcoreProgressRoot.SetAsLastSibling();

                foreach (var t in m_hardcoreProgressTranforms)
                {
                    t.SetParent(m_landscapeHardcoreProgressRoot);
                }

                m_dummyMapRectTransform = (RectTransform)m_landscapeDummyMap;

                var screenWidth = Screen.safeArea.width / Screen.safeArea.height;
                var mapSize = 1;
                var sideBarSize = (428 - 320) / 320f;
                var sideMargin = (screenWidth - (mapSize + sideBarSize)) / 2;
                var spaceSize = 8 / 320f; // Between map and right side bar
                spaceSize = sideMargin * 0.5f > spaceSize ? spaceSize : 0;

                var rightBarAnchorMin = m_rightSideBarRectTransform.anchorMin;
                rightBarAnchorMin.x = (sideMargin + mapSize + spaceSize * 0.5f) / screenWidth;
                m_rightSideBarRectTransform.anchorMin = rightBarAnchorMin;

                var anchorMin = m_dummyMapRectTransform.anchorMin;
                anchorMin.x = (sideMargin - spaceSize * 0.5f) / screenWidth;
                m_dummyMapRectTransform.anchorMin = anchorMin;

                var anchorMax = m_dummyMapRectTransform.anchorMax;
                anchorMax.x = (sideMargin - spaceSize * 0.5f + mapSize) / screenWidth;
                m_dummyMapRectTransform.anchorMax = anchorMax;
            }
            else
            {
                m_pauseButton.transform.SetParent(m_portraitPauseMenuButtonRoot, false);
                m_life.transform.SetParent(m_portraitLifeRoot);

                foreach (var t in m_headerTransforms)
                {
                    t.SetParent(m_portraitHeaderRoot);
                }

                foreach (var t in m_hardcoreProgressTranforms)
                {
                    t.SetParent(m_portraitHardcoreProgressRoot);
                }

                m_dummyMapRectTransform = (RectTransform)m_portraitDummyMap;
            }
        }

        public void ShowProgressTime(int time)
        {
            m_stringBuilder.Length = 0;
            m_stringBuilder.Append(time);
            m_progressTimeText.text = m_stringBuilder.ToString();
        }

        public void GetViewportCorners(Vector3[] viewportCorners, Camera uiCamera)
        {
            var worldCorners = new Vector3[4];
            m_dummyMapRectTransform.GetWorldCorners(worldCorners);

            for (var i = 0; i < worldCorners.Length; ++i)
            {
                viewportCorners[i] = uiCamera.WorldToViewportPoint(worldCorners[i]);
            }
        }

        private void Awake()
        {
            EventManager.RegisterHandler<EventStageStarted>(OnStageStarted);
            EventManager.RegisterHandler<EventStageUnloaded>(OnStageUnloaded);
            EventManager.RegisterHandler<EventItemAcquired>(OnItemAcquired);
            EventManager.RegisterHandler<EventLifeCountChanged>(OnLifeCountChanged);

            HideUI();
            UpdateLife();

            UpdateUIByScreenOrientationChange(Screen.width > Screen.height);
        }

        private void OnDestroy()
        {
            EventManager.UnregisterHandler<EventStageStarted>(OnStageStarted);
            EventManager.UnregisterHandler<EventStageUnloaded>(OnStageUnloaded);
            EventManager.UnregisterHandler<EventItemAcquired>(OnItemAcquired);
            EventManager.UnregisterHandler<EventLifeCountChanged>(OnLifeCountChanged);
        }

        private void OnStageStarted(EventStageStarted eventData)
        {
            ShowUI();

            if (m_showHeader)
            {
                m_doorKeyCount = 0;
                m_maxDoorKeyCount = eventData.MaxDoorKeyCount;
                SetDoorKeyCountText(m_doorKeyCount, m_maxDoorKeyCount);

                var stageNumber = PlayerDataManager.Instance.GetStageIndex(eventData.WorldId, eventData.StageId) + 1;
                SetStageText(stageNumber);
            }
        }

        private void OnStageUnloaded(EventStageUnloaded eventData)
        {
            HideUI();
        }

        private void OnItemAcquired(EventItemAcquired eventData)
        {
            if (eventData.itemType != ItemType.DoorKey) return;
            m_doorKeyCount++;
            SetDoorKeyCountText(m_doorKeyCount, m_maxDoorKeyCount);
        }

        public void OnPauseButtonClicked() => OpenPauseMenu();

        private void OnLifeCountChanged(EventLifeCountChanged eventData)
        {
            UpdateLife(eventData.LifeCount);
        }

        private void UpdateLife() => UpdateLife(PlayerDataManager.Instance.LifeCount);

        private void UpdateLife(int lifeCount)
        {
            for (var i = 0; i < m_lives.Length; i++)
            {
                m_lives[i].SetActive(i < lifeCount);
            }
        }

        private void OpenPauseMenu()
        {
            UIManager.Instance.Open(m_pauseMenuPrefab);
        }

        private void SetDoorKeyCountText(int doorKeyCount, int maxDoorKeyCount) => m_doorKeyCountText.SetText($"{doorKeyCount}/{maxDoorKeyCount}");

        private void SetStageText(int stageNumber) => m_stageNumberText.SetText($"{stageNumber}");

        private void ShowUI()
        {
            m_pauseButton.SetActive(m_showPauseButton);

            if (m_showLife)
            {
                UpdateLife();
            }
            else
            {
                foreach (var life in m_lives)
                {
                    life.SetActive(false);
                }
            }

            m_portraitHeaderRoot.gameObject.SetActive(m_showHeader);
            m_landscapeHeaderRoot.gameObject.SetActive(m_showHeader);

            if (GameController.Instance.GameType == GameType.Hardcore)
            {
                m_portraitHardcoreProgressRoot.gameObject.SetActive(true);
                m_landscapeHardcoreProgressRoot.gameObject.SetActive(true);

                var rate = GameController.Instance.GetClearedRandomStageRate();
                Debug.Log($"ClearedRandomStageRate:{rate}");
                m_hardcoreSlider.value = rate;
                m_hardcoreProgressText.text = $"{(int)(rate * 100)}%";
            }
        }

        private void HideUI()
        {
            m_pauseButton.SetActive(false);
            foreach (var life in m_lives)
            {
                life.SetActive(false);
            }
            m_portraitHeaderRoot.gameObject.SetActive(false);
            m_landscapeHeaderRoot.gameObject.SetActive(false);
            m_portraitHardcoreProgressRoot.gameObject.SetActive(false);
            m_landscapeHardcoreProgressRoot.gameObject.SetActive(false);
        }
    }
}
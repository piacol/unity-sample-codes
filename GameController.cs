using System.Linq;

namespace Platformer
{
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityEngine.UI;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using UI;

    public struct StageInfo
    {
        public int WorldId;
        public int StageId;
    }

    public class GameController : MonoBehaviour
    {
        [SerializeField] private GameObject m_playerPrefab = null;
        [SerializeField] private GameObject m_monsterPrefab = null;
        [SerializeField] private CameraController m_cameraController = null;
        [SerializeField] private GameObject m_inGameUIPrefab = null;

        private const int FPS = 60;
        public const float InverseFPS = 1f / FPS;
        private const float LimitedTime = 60;
        private PlayerDataManager m_playerDataManager;
        private AdManager m_adManager;
        private StageLoader m_stageLoader = null;
        private LinkedList<Mover> m_movers = new LinkedList<Mover>();
        private Player m_player;
        private List<Monster> m_monsters = new List<Monster>();
        private List<Commander> m_commanders = new List<Commander>();
        private float m_elapsedTime = 0;
        private GameObject m_startPoint;
        private NewStateMachine<GameState> m_newStateMachine;
        private float m_progressTime = 0;
        private float m_prevTime;
        private GameObject m_ingameUI;
        private HUD m_hud;
        private FadeTransition m_fadeTransition;
        private int m_worldId;
        private int m_stageId;
        private int m_maxCoinCount;
        private int m_maxDoorKeyCount;
        private GameResultState m_gameResultState;
        private bool m_paused;
        private UIManager m_uiManager;
        private GameType m_gameType;
        private List<StageInfo> m_randomStageInfos;
        private int m_totalStageCount;

        public static GameController Instance { get; private set; }
        public CommanderInput CommanderInput { get; private set; }
        public LinkedList<Mover> Movers => m_movers;
        public Player Player => m_player;
        public GameResultState GameResultState => m_gameResultState;
        public int MonsterCount => m_monsters.Count;
        public HUD HUD => m_hud;
        public float ProgressTime { get => m_progressTime; set => m_progressTime = value; }
        public int WorldId => m_worldId;
        public int StageId { get => m_stageId; set => m_stageId = value; }
        public Action<int> OnTickUpdated;
        public StageLoader StageLoader => m_stageLoader;
        public GameType GameType => m_gameType;

        public IEnumerator Init(UIManager uiManager, PlayerDataManager playerDataManager, StageDef[] stageDefs, int worldId, int stageId, GameType gameType, AdManager adManager = null)
        {
            m_uiManager = uiManager;
            m_playerDataManager = playerDataManager;
            m_adManager = adManager;
            m_worldId = worldId;
            m_stageId = stageId;
            m_gameType = gameType;

            m_stageLoader = new StageLoader(stageDefs);

            m_newStateMachine = new NewStateMachine<GameState>();
            m_newStateMachine.AddState(new GameStateReady(this, 2000));
            m_newStateMachine.AddState(new GameStateOnGoing(this, 60000));
            m_newStateMachine.AddState(new GameStateTimerExpired(this));

            var endTime = 0;
            if (GameType == GameType.Normal)
            {
                endTime = m_playerDataManager.IsDemo ? 500 : 2000;// 500: Wait for GoInside animation to be finished.
            }
            m_newStateMachine.AddState(new GameStateResult(this, m_playerDataManager, endTime));

            if (m_gameType == GameType.Hardcore)
            {
                m_randomStageInfos = new List<StageInfo>();
                ResetRandomStageInfos();
                UpdateWorldIdAndStageIdRandomly();
            }

            if (m_playerPrefab != null)
            {
                var go = Instantiate(m_playerPrefab);
                if (go != null)
                {
                    go.transform.parent = transform;
                    go.SetActive(false);

                    var actor = go.GetComponent<Player>();
                    Commander commander = null;

                    if (m_playerDataManager.IsDemo)
                    {
                        commander = go.AddComponent<CommanderDemoInput>();
                    }
                    else if (m_playerDataManager.IsReplay)
                    {
                        commander = go.AddComponent<CommanderReplayInput>();
                    }
                    else
                    {
                        commander = go.AddComponent<CommanderInput>();
                    }

                    commander.Init(actor);
                    CommanderInput = commander as CommanderInput;
                    m_player = actor;
                }
            }

            CreateInGameUI();

            yield return LoadLevel(m_stageId);
            yield return new WaitForSeconds(0.75f);

            Fade(FadeType.In);
        }

        private void ResetRandomStageInfos()
        {
            m_randomStageInfos.Clear();

            var worldCount = PlayerDataManager.Instance.GetWorldCount();
            for (var i = 0; i < worldCount; i++)
            {
                var wId = i + 1;
                var stageCount = PlayerDataManager.Instance.GetStageCount(wId);
                for (var j = 0; j < stageCount; j++)
                {
                    var sId = PlayerDataManager.Instance.GetStageId(wId, j);
                    StageInfo stageInfo;
                    stageInfo.WorldId = wId;
                    stageInfo.StageId = sId;
                    m_randomStageInfos.Add(stageInfo);
                }
            }

            m_totalStageCount = m_randomStageInfos.Count;
        }

        private void OnEnable()
        {
            EventManager.RegisterHandler<EventScreenOrientationChanged>(OnScreenOrientationChanged);
        }

        private void OnDisable()
        {
            EventManager.UnregisterHandler<EventScreenOrientationChanged>(OnScreenOrientationChanged);
        }

        public void Fade(FadeType type, Action onFinishedCallback = null)
        {
            if (m_fadeTransition)
            {
                m_fadeTransition.Fade(type, onFinishedCallback);
            }
        }

        public void AddCommander(Commander commander) => m_commanders.Add(commander);

        public void RemoveCommander(Commander commander) => m_commanders.Remove(commander);

        public int AddMover(Mover mover)
        {
            m_movers.AddLast(mover);
            return m_movers.Count;
        }

        public void RemoveMover(Mover mover) => m_movers.Remove(mover);

        public void RestartGame()
        {
            SetGameResultState(GameResultState.Aborted);
            ChangeState(GameState.Result, true);
        }

        public void QuitGame()
        {
            SetGameResultState(GameResultState.Aborted);
            ChangeState(GameState.Result);
        }

        public void UpdateProgressTime(float dt)
        {
            if (m_playerDataManager.IsTutorialGameplay)
            {
                return;
            }

            ProgressTime += dt;
            if (ProgressTime >= LimitedTime)
            {
                ChangeState(GameState.TimerExpired);
            }

            if ((int)ProgressTime != (int)m_prevTime)
            {
                HUD?.ShowProgressTime(GetRemainedTime());
            }
            m_prevTime = ProgressTime;
        }

        public void PauseGame()
        {
            m_paused = true;
            EventManager.Send(EventGamePaused.Create());
        }

        public void ResumeGame()
        {
            m_paused = false;
            EventManager.Send(EventGameResumed.Create());
        }

        public void Destroy()
        {
            var stateType = GetStateType();
            if (stateType == GameState.OnGoing || stateType == GameState.TimerExpired)
            {
                m_playerDataManager.AbortGame(GetRemainedTime());
            }

            DestroyInGameUI();

            m_movers.Clear();
            m_player = null;

            EventManager.UnregisterHandler<EventPlayerGoInsideEnded>(OnPlayerGoInsideEnded);
            EventManager.UnregisterHandler<EventPlayerDeathEnded>(OnPlayerDeathEnded);
        }

        private void Awake()
        {
            EventManager.RegisterHandler<EventPlayerGoInsideEnded>(OnPlayerGoInsideEnded);
            EventManager.RegisterHandler<EventPlayerDeathEnded>(OnPlayerDeathEnded);

            DontDestroyOnLoad(gameObject);

            Instance = this;

            if(m_monsterPrefab == null)
            {
                Debug.LogError("GameController.Awake() Failed - m_monsterPrefab is null");
            }
        }

        private void Update()
        {
            if (m_paused) return;
            m_elapsedTime += Time.deltaTime;

            var frameCount = 0;
            var dt = InverseFPS;

            if (m_elapsedTime > dt)
            {
                frameCount = (int)(m_elapsedTime / dt);

                m_elapsedTime -= (float)frameCount * dt;
            }
            else
            {
                return;
            }

            for (var n = 0; n < frameCount; n++)
            {
                m_newStateMachine.Update(dt);
            }

            if (GetStateType() == GameState.OnGoing ||
                GetStateType() == GameState.TimerExpired)
            {
                LinkedListNode<Mover> node;
                LinkedListNode<Mover> nextNode;
                Mover mover;

                for (var n = 0; n < frameCount; n++)
                {
                    var commanderCount = m_commanders.Count;
                    for (var i = 0; i < commanderCount; i++)
                    {
                        m_commanders[i].CustomUpdate(dt);
                    }

                    for (node = m_movers.First; node != null; node = nextNode)
                    {
                        nextNode = node.Next;
                        mover = node.Value;

                        if (!mover.gameObject.activeSelf || !mover.enabled)
                        {
                            continue;
                        }
                        mover.CustomUpdate(dt);
                    }

                    OnTickUpdated?.Invoke(m_playerDataManager.Tick);
                    m_playerDataManager.Tick++;

                    if (m_paused)
                    {
                        break;
                    }
                }
            }
        }

        private void ResetGame()
        {
            m_progressTime = 0;
            InitLevel();
            EventManager.Send(EventStageStarted.Create(GameType, m_worldId, m_stageId, m_maxCoinCount, m_maxDoorKeyCount));
            ChangeState(GameState.Ready);
        }

        private void InitLevel()
        {
            InitCameraController();

            m_player.gameObject.SetActive(true);
            var direction = eActorDirection.Right;
            m_startPoint = GameObject.FindGameObjectWithTag("StartPoint");
            if (m_startPoint != null)
            {
                m_player.PlaceAt(m_startPoint.transform.position);
                direction = m_startPoint.transform.localScale.x > 0 ? eActorDirection.Right : eActorDirection.Left;
            }

            var wp = m_cameraController.ViewportToWorldPoint(new Vector3(0, 0, 0));
            m_player.Init(direction, wp.y);

            m_maxCoinCount = 0;
            m_maxDoorKeyCount = 0;

            foreach(var mover in Movers)
            {
                if(mover.type != Mover.Type.Item)
                {
                    continue;
                }

                var item = (Item)mover;
                if(item.ItemType == ItemType.Coin)
                {
                    m_maxCoinCount++;
                }
                else if(item.ItemType == ItemType.DoorKey)
                {
                    m_maxDoorKeyCount++;
                }
            }

            foreach(var mover in Movers)
            {
                if(mover is Monster monster)
                {
                    monster.PlaceAt(monster.Position);
                }
            }
        }

        private void InitCameraController()
        {
            if (m_cameraController != null)
            {
                var min = new Vector3(float.MaxValue, float.MaxValue, 0);
                var max = new Vector3(float.MinValue, float.MinValue, 0);
                GetMinMaxMoverPosition(ref min, ref max, Mover.Type.Platform);

                var viewportCorners = new Vector3[4];
                m_hud.GetViewportCorners(viewportCorners, m_uiManager.UICamera);

                var safeAreaScale = Screen.width > Screen.height
                    ? Screen.height / Screen.safeArea.height
                    : Screen.width / Screen.safeArea.width;
                m_cameraController.Init(new Vector2(Screen.width, Screen.height), min, max, safeAreaScale, viewportCorners);
            }
        }

        private void OnPlayerGoInsideEnded(EventPlayerGoInsideEnded eventData)
        {
            if (GetStateType() == GameState.OnGoing ||
                GetStateType() == GameState.TimerExpired)
            {
                EndGame(GameState.Result, GameResultState.Cleared);
            }
        }

        private void OnPlayerDeathEnded(EventPlayerDeathEnded eventData) => EndGame(GameState.Result, GameResultState.Failed);

        public void LoadLevel() => StartCoroutine(LoadLevel(m_stageId));

        private IEnumerator LoadLevel(int stageID)
        {
            m_stageLoader.UnloadStage();
            DestroyMonster();
            m_player.gameObject.SetActive(false);

            Debugging.Log($"LoadLevel() - stageID:{stageID}");

            var asyncLoad = SceneManager.LoadSceneAsync("EmptyScene");
            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            asyncLoad = SceneManager.LoadSceneAsync("Gameplay");
            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            m_stageLoader.LoadStage(stageID);
            ResetGame();
        }

        private void EndGame(GameState gameState, GameResultState gameResultState)
        {
            SetGameResultState(gameResultState);
            ChangeState(gameState);
        }

        public int GetRemainedTime()
        {
            var result = (int)LimitedTime - (int)m_progressTime;
            result = result > 0 ? result : 0;
            return result;
        }

        private void GetMinMaxMoverPosition(ref Vector3 min, ref Vector3 max, Mover.Type type)
        {
            min.z = 0;
            max.z = 0;

            foreach (var mover in m_movers)
            {
                if (mover.type != type)
                {
                    continue;
                }

                var pos = mover.Position;
                var r = mover.Radius;

                if (pos.x - r.x < min.x)
                {
                    min.x = pos.x - r.x;
                }

                if (pos.y - r.y < min.y)
                {
                    min.y = pos.y - r.y;
                }

                if (pos.x + r.x > max.x)
                {
                    max.x = pos.x + r.x;
                }

                if (pos.y + r.y > max.y)
                {
                    max.y = pos.y + r.y;
                }
            }
        }

        public void SpawnMonster()
        {
            Monster monster = null;
            var go = GameObject.Instantiate(m_monsterPrefab);
            if (go != null)
            {
                go.transform.parent = transform;

                monster = go.GetComponent<Monster>();
            }

            var commander = go.GetComponent<Commander>();
            commander.Init(monster);

            monster.PlaceAt(m_startPoint.transform.position);

            var direction = m_startPoint.transform.localScale.x > 0 ? eActorDirection.Right : eActorDirection.Left;
            monster.Init(direction);

            m_monsters.Add(monster);
        }

        public bool GoToNextStage()
        {
            var nextStageId = GetNextStageId();
            if (nextStageId == -1)
            {
                var nextWorldId = GetNextWorldId();
                if (nextWorldId == -1)
                {
                    return false;
                }

                UpdateWorld(nextWorldId);

                nextStageId = PlayerDataManager.Instance.GetStageId(m_worldId, 0);
            }

            if (!m_playerDataManager.ValidateTotalStarCount(m_worldId, nextStageId))
            {
                EventManager.Send(RequestOpenNotEnoughStars.Create());
                return true;
            }

            StageId = nextStageId;
            LoadLevel();
            return true;
        }

        public void UpdateWorldIdAndStageIdRandomly()
        {
            if (m_randomStageInfos.Count == 0)
            {
                return;
            }

            var randIndex = UnityEngine.Random.Range(0, m_randomStageInfos.Count);
            var newWorldId = m_randomStageInfos[randIndex].WorldId;
            var newStageId = m_randomStageInfos[randIndex].StageId;

            m_randomStageInfos.RemoveAt(randIndex);

            if (m_worldId != newWorldId)
            {
                UpdateWorld(newWorldId);
            }

            StageId = newStageId;
        }

        private void UpdateWorld(int nextWorldId)
        {
            m_worldId = nextWorldId;
            var stageDefs = PlayerDataManager.Instance.GetStageDefs(m_worldId);
            m_stageLoader = new StageLoader(stageDefs);
        }

        public int GetNextStageId()
        {
            var index = PlayerDataManager.Instance.GetStageIndex(m_worldId, StageId);
            index++;

            var nextStageId = -1;
            if (index < PlayerDataManager.Instance.GetStageCount(m_worldId))
            {
                nextStageId = PlayerDataManager.Instance.GetStageId(m_worldId, index);
            }
            return nextStageId;
        }

        public void RetryStage() => LoadLevel();

        public void RetryHardcore()
        {
            PlayerDataManager.Instance.CurrentHardcoreResultData.Clear();
            ResetRandomStageInfos();
            UpdateWorldIdAndStageIdRandomly();
            LoadLevel();
        }

        public int GetRandomStageInfoCount() => m_randomStageInfos.Count;

        public float GetClearedRandomStageRate()
        {
            Debug.Log($"{PlayerDataManager.Instance.CurrentHardcoreResultData.ClearedStageCount} / {m_totalStageCount}");
            return (float)PlayerDataManager.Instance.CurrentHardcoreResultData.ClearedStageCount / m_totalStageCount;
        }

        private int GetNextWorldId()
        {
            var nextWorldId = m_worldId + 1;
            if (PlayerDataManager.Instance.ValidateWorldId(nextWorldId))
            {
                return nextWorldId;
            }
            return -1;
        }

        private void DestroyMonster()
        {
            for(var i = 0; i < m_monsters.Count; ++i)
            {
                Destroy(m_monsters[i].gameObject);
            }
            m_monsters.Clear();
        }

        public void ChangeState(GameState state, bool exitImmediately = false)
        {
            var previousState = GetStateType();
            m_newStateMachine.ChangeState(state, exitImmediately);
            var worldNumber = m_playerDataManager.GetWorldNumber(m_worldId);
            EventManager.Send(EventGameStateChanged.Create(state, previousState, worldNumber));
        }

        private GameState GetStateType()
        {
            if (m_newStateMachine.State != null)
            {
                return m_newStateMachine.State.StateType;
            }
            return GameState.Invalid;
        }

        private void SetGameResultState(GameResultState state) => m_gameResultState = state;

        private void OnScreenOrientationChanged(EventScreenOrientationChanged eventData)
        {
            UpdateUIAndCameraByNewScreenOrientation(eventData.IsLandscape);
        }

        private void UpdateUIAndCameraByNewScreenOrientation(bool isLandscape)
        {
            m_hud.UpdateUIByScreenOrientationChange(isLandscape);
            InitCameraController();
        }

        private void DestroyInGameUI()
        {
            if (m_ingameUI)
            {
                Destroy(m_ingameUI);
                m_ingameUI = null;
            }
        }

        private void CreateInGameUI()
        {
            m_ingameUI = Instantiate(m_inGameUIPrefab, m_uiManager.UIRoot.transform);
            m_ingameUI.transform.SetSiblingIndex(0);

            var canvas = m_ingameUI.GetComponentInChildren<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = m_uiManager.UICamera;
            canvas.sortingOrder = 0;

            m_hud = m_ingameUI.GetComponentInChildren<HUD>();
            m_fadeTransition = m_ingameUI.GetComponentInChildren<FadeTransition>(true);

            var tutorialGameplayController = GetComponent<TutorialGameplayController>();
            if (tutorialGameplayController != null)
            {
                tutorialGameplayController.Init();
            }
        }
    }
}
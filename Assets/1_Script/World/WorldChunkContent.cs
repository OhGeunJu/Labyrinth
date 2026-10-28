using System;
using UnityEngine;

// **청크 하나에 놓인 것 전부**(2026-08-27) — `Resources/WorldContent/{x}_{y}.asset`.
//
// `TerrainDelta`의 형제다. 지형이 *"손댄 정점만 청크별 에셋"*인 것과 같은 이유로, 세계에 놓인
// 물건도 **청크별 에셋**이 된다.
//
// ── 왜 씬 배열에서 내려왔나 ─────────────────────────────────────────────────
//
// 전에는 `WorldContentStreamer.sites`라는 **씬에 직렬화된 배열 하나**였다. 거기서 셋이
// 한꺼번에 나왔다:
//
//   ① **씬 파일이 붓는다.** 물건이 수백 개면 `Scene1.unity`가 그만큼 커지고, 유니티를 켜 둔
//      채로는 손도 못 댄다(직접 고쳐 봐야 저장 한 번에 되돌아간다).
//   ② **조회가 전수 훑기다.** 청크가 로드될 때마다 배열을 처음부터 끝까지 훑었다 —
//      걸어 다니면 청크가 바뀔 때마다 매번.
//   ③ **인덱스가 신원 노릇을 했다.** `Persistent` 추적이 배열 인덱스였는데, 저작으로 배열이
//      재정렬되면(`5. Place Sites`가 실제로 앞으로 당긴다) 그 순번이 가리키던 곳이 달라진다.
//
// 청크별 에셋 하나로 셋이 다 풀린다 — 씬에서 빠지고 · 조회가 O(1)이고 · **신원이 `Id`다.**
//
// **JSON이 아니라 SO인 이유**는 이 저장소의 기준 그대로다(`docs/data.md`):
// *"다른 에셋이 GUID로 가리켜야 하는가"*. 프리팹 참조가 **필수**라 JSON으로는 가리킬 방법이 없다.
public class WorldChunkContent : ScriptableObject
{
    // **경로를 아는 곳은 여기 하나다** — 런타임(`Resources.Load`)과 저작 도구가 같은 답을
    // 써야 한다. 갈리면 도구가 쓴 자리를 런타임이 안 읽는다.
    public const string ResourceDir = "WorldContent";

    public static string ResourcePathOf(Vector2Int chunk) =>
        ResourceDir + "/" + TerrainGrid.KeyOf(chunk);

    public Vector2Int    Chunk;
    public WorldObject[] Objects = Array.Empty<WorldObject>();
}

// 세계에 놓인 것 하나. **`WorldSite`가 이걸로 승격됐다** — `Id`·`Spec`·`QuestId`가 늘었고,
// 사는 곳이 씬 배열에서 청크 에셋으로 옮겨졌다.
[Serializable]
public class WorldObject
{
    // **신원은 순번이 아니다.** `Persistent` 추적이 이걸 키로 쓰고, 나중에 세이브가
    // *"이 마을은 이미 털렸다"*를 기억하려면 저작이 바뀌어도 안 변하는 것이어야 한다.
    //
    // 도구가 지도에서 뽑은 것은 **결정적**으로 짓는다(`map_{종류}_{지역x}_{지역y}`) — 그래야
    // `5. Place Sites`를 다시 돌려도 같은 소굴이 같은 신원을 유지한다. 손으로 놓은 것은 GUID.
    public string Id;

    public string     Name = "장소";
    public GameObject Prefab;

    // **무엇인가** — 마을·소굴·자원 지대. **`Empty`면 그냥 물건이다**(장식·소품·폰) —
    // 지도에 안 뜼고 장소 색인(`WorldSites`)에도 안 든다.
    //
    // 전에는 **저작한 격자가 이걸 들고 있었다**(`WorldMapData.Node.Content`). 그래서 손으로 놓은
    // 마을은 **게임 지도에 안 떴다** — 격자에 안 찍혔으니까. 물건이 제 종류를 들면
    // **세계에 실제로 있는 것**이 그대로 화면에 뜼고, 격자에 매이지도 않는다
    // (한 지역에 소굴 하나만 놓을 수 있던 제한이 없어진다).
    public WorldNodeContent Kind;

    // **월드 좌표 XZ만이다 — Y는 없다.**
    //
    // 높이는 런타임에 `TerrainHeight.At()`으로 지면에 붙인다. 절대 Y로 저장하면 **지형을 다시
    // 조각한 순간 마을이 공중에 뜨거나 파묻힌다.** 딸려 오는 이득이 하나 더 있다 — 저작할 때
    // 정확한 지형이 없어도 데이터가 맞는다(XZ와 방향만 정하면 된다).
    public Vector2 Position;

    [Tooltip("지면에서 띄울 높이(m). 0이면 지면에 딱 붙는다.")]
    public float YOffset;

    [Range(0f, 360f)] public float Yaw;

    // **씬에서 조절한 크기.** 없으면(= `Vector3.zero`) **손대지 않는다** — 프리팹이 제 크기를
    // 들고 있을 수 있어서, 없는 것을 1로 치면 **한 번도 안 건드린 물건이 굽는 순간 크기가
    // 바뀐다.** 옛 데이터(1단계에서 옮겨 온 것)가 정확히 그 상태다.
    //
    // **기울기는 저장하지 않는다** — 물건은 지면 위에 서고 도는 것은 수평뿐이다(`Yaw`).
    // 씬에서 기울이면 굽기가 경고한다. 조용히 되돌리지는 않는다.
    public Vector3 Scale;

    [Tooltip("켜면 한 번 뜬 뒤로는 안 사라진다. 마을처럼 상태를 들고 있는 곳에.")]
    public bool Persistent = true;

    // **도구가 낸 것인가.** 켜져 있으면 `5. Populate World`가 다시 돌 때 통째로 갈아 끼운다.
    // 꺼진 것(= 손으로 놓은 것)은 **아예 안 본다** — 그게 없으면 자동 배치를 한 번 돌릴 때마다
    // 손으로 지은 도시가 사라져 **다시 돌릴 없두가 안 난다.**
    [UnityEngine.Serialization.FormerlySerializedAs("FromMap")]
    public bool Generated;

    // 폰이면 — 비우면 태어날 때 굴린다. **아직 아무도 안 읽는다**(월드 에디터 4단계).
    // 그릇을 먼저 둔 이유는 저작 씬에서 굽는 쪽과 형식을 맞추기 위해서다.
    public PawnSpecData Spec;

    // 훅. **아직 아무도 안 읽는다** — 퀘스트 시스템이 통째로 없다(`docs/design_world_editor.md`).
    public string QuestId;
}

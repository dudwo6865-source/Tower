using System;
using UnityEngine;

// 언덕(Hill) 전용 타일 세트입니다.
// H 칸만 칠하면 주변 벽/코너가 WxH·HxW·HXH/HXW In/Out으로 자동 교체됩니다.
[CreateAssetMenu(fileName = "HillTileSet", menuName = "Tank/언덕 타일 세트")]
public class HillTileSet : ScriptableObject
{
    // 3x3 중심 기준 8방위 (시계: N → NE → E → … → NW)
    public enum Dir8
    {
        N = 0,
        NE = 1,
        E = 2,
        SE = 3,
        S = 4,
        SW = 5,
        W = 6,
        NW = 7,
    }

    [Serializable]
    public struct SidePose
    {
        [Label("Y 회전")]
        [Tooltip("Y 회전(도).")]
        public float yaw;

        [Label("X 반전")]
        [Tooltip("localScale.x 반전.")]
        public bool flipX;
    }

    [Serializable]
    public struct FacingPoses
    {
        [Label("Hill 왼쪽")]
        [Tooltip("벽을 바깥으로 볼 때 Hill이 왼쪽.")]
        public SidePose left;

        [Label("Hill 오른쪽")]
        [Tooltip("벽을 바깥으로 볼 때 Hill이 오른쪽.")]
        public SidePose right;
    }

    [Serializable]
    public struct Dir8Poses
    {
        [Label("북 (N)")]
        public SidePose n;
        [Label("북동 (NE)")]
        public SidePose ne;
        [Label("동 (E)")]
        public SidePose e;
        [Label("남동 (SE)")]
        public SidePose se;
        [Label("남 (S)")]
        public SidePose s;
        [Label("남서 (SW)")]
        public SidePose sw;
        [Label("서 (W)")]
        public SidePose w;
        [Label("북서 (NW)")]
        public SidePose nw;

        public SidePose Get(Dir8 dir)
        {
            return dir switch
            {
                Dir8.N => n,
                Dir8.NE => ne,
                Dir8.E => e,
                Dir8.SE => se,
                Dir8.S => s,
                Dir8.SW => sw,
                Dir8.W => w,
                _ => nw,
            };
        }

        public void Set(Dir8 dir, SidePose pose)
        {
            switch (dir)
            {
                case Dir8.N: n = pose; break;
                case Dir8.NE: ne = pose; break;
                case Dir8.E: e = pose; break;
                case Dir8.SE: se = pose; break;
                case Dir8.S: s = pose; break;
                case Dir8.SW: sw = pose; break;
                case Dir8.W: w = pose; break;
                default: nw = pose; break;
            }
        }
    }

    [Header("프리팹")]
    [Label("언덕 (H)")]
    [Tooltip("직선 벽 자리에 칠한 언덕 (H).")]
    public GameObject hill;

    [Label("벽-언덕 연결 (WxH)")]
    [Tooltip("직선 벽끼리 연결 (WxH). HxW는 X스케일 반전.")]
    public GameObject wxh;

    [Label("HxW (사용 안 함)")]
    [Tooltip("더 이상 사용하지 않습니다. HxW는 WxH의 localScale.x를 반전해서 만듭니다.")]
    public GameObject hxw;

    [Label("Hill 왼쪽이면 WxH 반전")]
    [Tooltip("체크 시 WxH(직선)에서 Hill이 왼쪽일 때 X미러.")]
    public bool flipWxhWhenHillOnLeft;

    [Label("HXW 좌우 바꾸기")]
    [Tooltip("HXW 좌/우 방향표 조회를 서로 바꿉니다.")]
    public bool invertHxwLeftRight;

    [Label("HXW 바깥 코너")]
    [Tooltip("직선 벽 위 Hill + 옆이 바깥 코너일 때 → HXW_Out")]
    public GameObject hxwOuter;

    [Label("HXW 안쪽 코너")]
    [Tooltip("직선 벽 위 Hill + 옆이 안쪽 코너일 때 → HXW_In")]
    public GameObject hxwInner;

    [Label("바깥 코너 연결 (HXH_Out)")]
    [Tooltip("바깥 코너에 Hill을 칠했거나, 코너↔코너 연결 → HXH_Out")]
    public GameObject toOuterCorner;

    [Label("안쪽 코너 연결 (HXH_In)")]
    [Tooltip("안쪽 코너에 Hill을 칠했거나, 코너↔코너 연결 → HXH_In")]
    public GameObject toInnerCorner;

    [Header("오프셋")]
    [Label("언덕 높이 오프셋")]
    public float hillHeightOffset;
    [Label("연결부 높이 오프셋")]
    public float connectorHeightOffset;

    [Header("회전 오프셋 (Y축, 도)")]
    [Label("언덕 회전 오프셋")]
    public float hillYawOffset;
    [Label("WxH 회전 오프셋")]
    public float wxhYawOffset;
    [Label("HxW 회전 오프셋")]
    public float hxwYawOffset;
    [Label("바깥 코너 연결 회전 오프셋")]
    public float toOuterCornerYawOffset;
    [Label("안쪽 코너 연결 회전 오프셋")]
    public float toInnerCornerYawOffset;

    [Header("HXW 코너↔코너 8방위 (Hill 로컬)")]
    [Label("바깥 코너↔코너 방위표")]
    [Tooltip("Hill 벽 바깥을 위(N)로 둔 상대 방위. 어느 쪽 벽에 설치해도 같은 항목이 씁니다. Yaw=edgeYaw 오프셋.")]
    public Dir8Poses hxwOuterCornerToCorner;

    [Label("안쪽 코너↔코너 방위표")]
    [Tooltip("Inner용. 동일하게 Hill 벽 바깥 = 로컬 N.")]
    public Dir8Poses hxwInnerCornerToCorner;

    [Header("HXW 바깥 방향표 (벽↔코너, Hill 좌/우)")]
    [Label("바깥 북")]
    [Tooltip("옆이 직선 벽(W)일 때만 사용. 절대 Yaw + FlipX.")]
    public FacingPoses hxwOuterNorth;
    [Label("바깥 동")]
    public FacingPoses hxwOuterEast;
    [Label("바깥 남")]
    public FacingPoses hxwOuterSouth;
    [Label("바깥 서")]
    public FacingPoses hxwOuterWest;

    [Header("HXW 안쪽 방향표 (벽↔코너, Hill 좌/우)")]
    [Label("안쪽 북")]
    public FacingPoses hxwInnerNorth;
    [Label("안쪽 동")]
    public FacingPoses hxwInnerEast;
    [Label("안쪽 남")]
    public FacingPoses hxwInnerSouth;
    [Label("안쪽 서")]
    public FacingPoses hxwInnerWest;

    // wallOutward: 0=N,1=E,2=S,3=W / hillOnRight: 벽을 바깥으로 볼 때 Hill이 오른쪽
    public bool TryGetHxwPose(bool outer, int wallOutward, bool hillOnRight, out float yaw, out bool flipX)
    {
        wallOutward = ((wallOutward % 4) + 4) % 4;
        FacingPoses facing = GetHxwFacing(outer, wallOutward);
        SidePose side = hillOnRight ? facing.right : facing.left;
        yaw = side.yaw;
        flipX = side.flipX;
        return true;
    }

    public bool TryGetHxwCornerToCornerPose(bool outer, Dir8 dir, out float yaw, out bool flipX)
    {
        Dir8Poses table = outer ? hxwOuterCornerToCorner : hxwInnerCornerToCorner;
        SidePose pose = table.Get(dir);
        yaw = pose.yaw;
        flipX = pose.flipX;
        return true;
    }

    FacingPoses GetHxwFacing(bool outer, int wallOutward)
    {
        if (outer)
        {
            return wallOutward switch
            {
                0 => hxwOuterNorth,
                1 => hxwOuterEast,
                2 => hxwOuterSouth,
                _ => hxwOuterWest,
            };
        }

        return wallOutward switch
        {
            0 => hxwInnerNorth,
            1 => hxwInnerEast,
            2 => hxwInnerSouth,
            _ => hxwInnerWest,
        };
    }

    void Reset()
    {
        ApplyDefaultHxwPoses();
    }

    [ContextMenu("HXW 방향표 기본값 채우기")]
    public void ApplyDefaultHxwPoses()
    {
        hxwOuterNorth = DefaultFacing(0f);
        hxwOuterEast = DefaultFacing(90f);
        hxwOuterSouth = DefaultFacing(180f);
        hxwOuterWest = DefaultFacing(270f);
        hxwInnerNorth = DefaultFacing(0f);
        hxwInnerEast = DefaultFacing(90f);
        hxwInnerSouth = DefaultFacing(180f);
        hxwInnerWest = DefaultFacing(270f);
        hxwOuterCornerToCorner = DefaultDir8();
        hxwInnerCornerToCorner = DefaultDir8();
    }

    static FacingPoses DefaultFacing(float wallYaw)
    {
        return new FacingPoses
        {
            left = new SidePose { yaw = wallYaw, flipX = false },
            right = new SidePose { yaw = wallYaw + 180f, flipX = false },
        };
    }

    static Dir8Poses DefaultDir8()
    {
        // 기본 오프셋 0 — 배치는 edgeYaw, 미세 조정만 표에서
        return new Dir8Poses
        {
            n = new SidePose { yaw = 0f },
            ne = new SidePose { yaw = 0f },
            e = new SidePose { yaw = 0f },
            se = new SidePose { yaw = 0f },
            s = new SidePose { yaw = 0f },
            sw = new SidePose { yaw = 0f },
            w = new SidePose { yaw = 0f },
            nw = new SidePose { yaw = 0f },
        };
    }
}

/* containing function of static patch target(s); entry .opd.FUN_00a87ab0 @ 00a87ab0 */

void _opd_FUN_00a87ab0(int param_1)

{
  byte *pbVar1;
  bool bVar2;
  byte bVar3;
  short sVar4;
  undefined2 uVar5;
  undefined1 uVar10;
  int iVar7;
  char cVar11;
  int iVar8;
  undefined8 uVar6;
  int iVar9;
  undefined4 uVar12;
  undefined4 *puVar13;
  char *pcVar14;
  byte *pbVar15;
  byte bVar16;
  byte *pbVar17;
  byte *pbVar18;
  longlong lVar19;
  
  sVar4 = *(short *)(param_1 + 0x68);
  if (sVar4 == 1) {
    uVar6 = _opd_FUN_00f07ae0(*(undefined4 *)(param_1 + 0x60),param_1 + 0x6c);
    if ((int)uVar6 == 0) {
      *(undefined4 *)(param_1 + 100) = 0;
      _opd_FUN_00a20530(uVar6,uVar6);
      uVar5 = 2;
      goto LAB_00a87f98;
    }
LAB_00a88404:
    uVar12 = (undefined4)uVar6;
    uVar6 = 0x1030;
LAB_00a8840c:
    _opd_FUN_0097f1b8(uVar6,uVar12,0);
    puVar13 = *(undefined4 **)(param_1 + 0x138);
    uVar12 = 0xffffffff;
LAB_00a88420:
    *puVar13 = uVar12;
  }
  else {
    if (sVar4 != 0) {
      if (sVar4 != 2) {
        if (sVar4 != 3) {
          return;
        }
        if (*(int *)(param_1 + 0x134) != 0) {
          _opd_FUN_00f9ac38(*(int *)(param_1 + 0x134),param_1 + 0x6c,200);
        }
        _opd_FUN_000c1e00(param_1);
        return;
      }
      iVar9 = *(int *)(param_1 + 100);
      uVar12 = 0xffffff60;
      uVar6 = 0x1031;
      *(int *)(param_1 + 100) = iVar9 + 5;
      if (iVar9 + 5U < 0x2ee1) {
        iVar9 = _opd_FUN_00f069c0(*(undefined4 *)(param_1 + 0x60),0xffffffffffffff60);
        if (iVar9 == 0) {
          return;
        }
        _opd_FUN_00a2059c();
        uVar6 = _opd_FUN_00f0672c(*(undefined4 *)(param_1 + 0x60));
        if ((int)uVar6 != 0) goto LAB_00a88404;
        **(undefined4 **)(param_1 + 0x138) = 0;
        goto LAB_00a88424;
      }
      goto LAB_00a8840c;
    }
    pbVar1 = (byte *)(param_1 + 0x6c);
    if (1 < *pbVar1) {
      *pbVar1 = 0;
    }
    if (10 < *(byte *)(param_1 + 0x6d)) {
      *(undefined1 *)(param_1 + 0x6d) = 0;
    }
    if ((0x15 < *(byte *)(param_1 + 0x6e)) || (*(byte *)(param_1 + 0x6e) < 0xb)) {
      *(undefined1 *)(param_1 + 0x6e) = 0xb;
    }
    if ((0x1b < *(byte *)(param_1 + 0x6f)) || (*(byte *)(param_1 + 0x6f) < 0x16)) {
      *(undefined1 *)(param_1 + 0x6f) = 0x16;
    }
    if (0x1f < *(byte *)(param_1 + 0x70)) {
      *(undefined1 *)(param_1 + 0x70) = 0;
    }
    if (0x1f < *(byte *)(param_1 + 0x71)) {
      iVar9 = _opd_FUN_008d2b58(*(undefined1 *)(param_1 + 0x6e),0,*pbVar1);
      uVar10 = 0;
      if (iVar9 != 0) {
        uVar10 = _opd_FUN_008d2708(*(undefined1 *)(param_1 + 0x6e),iVar9,*pbVar1);
      }
      *(undefined1 *)(param_1 + 0x71) = uVar10;
    }
    if (0x1f < *(byte *)(param_1 + 0x72)) {
      iVar9 = _opd_FUN_008d2b58(*(undefined1 *)(param_1 + 0x6f),0,*pbVar1);
      uVar10 = 0;
      if (iVar9 != 0) {
        uVar10 = _opd_FUN_008d2708(*(undefined1 *)(param_1 + 0x6f),iVar9,*pbVar1);
      }
      *(undefined1 *)(param_1 + 0x72) = uVar10;
    }
    bVar16 = *(byte *)(param_1 + 0x73);
    if (*pbVar1 == 0) {
      if ((bVar16 < 7) || (0xf < bVar16)) {
        uVar10 = 7;
LAB_00a87c50:
        *(undefined1 *)(param_1 + 0x73) = uVar10;
      }
    }
    else if ((bVar16 < 0x10) || (0x18 < bVar16)) {
      uVar10 = 0x10;
      goto LAB_00a87c50;
    }
    if (0x1f < *(byte *)(param_1 + 0x74)) {
      *(undefined1 *)(param_1 + 0x74) = 0xf;
    }
    iVar9 = _opd_FUN_00f06488(*(undefined4 *)(param_1 + 0x60));
    bVar16 = *(byte *)(param_1 + 0x7c);
    if (((0x2d < bVar16) || (bVar16 < 0x1c)) && ((0x9f < bVar16 || (-1 < (char)bVar16)))) {
      *(undefined1 *)(param_1 + 0x7c) = 0x1c;
    }
    bVar16 = *(byte *)(param_1 + 0x7d);
    if (((0x55 < bVar16) || (bVar16 < 0x44)) && ((0xd7 < bVar16 || (bVar16 < 0xc0)))) {
      *(undefined1 *)(param_1 + 0x7d) = 0x44;
    }
    bVar16 = *(byte *)(param_1 + 0x7e);
    if (((0x38 < bVar16) || (bVar16 < 0x2e)) && ((0xaf < bVar16 || (bVar16 < 0xa0)))) {
      *(undefined1 *)(param_1 + 0x7e) = 0x2e;
    }
    bVar16 = *(byte *)(param_1 + 0x7f);
    if (((0x65 < bVar16) || (bVar16 < 0x56)) && ((0xef < bVar16 || (bVar16 < 0xd8)))) {
      *(undefined1 *)(param_1 + 0x7f) = 0x56;
    }
    bVar16 = *(byte *)(param_1 + 0x80);
    if (((0x43 < bVar16) || (bVar16 < 0x39)) && ((0xbf < bVar16 || (bVar16 < 0xb0)))) {
      *(undefined1 *)(param_1 + 0x80) = 0x39;
    }
    bVar16 = *(byte *)(param_1 + 0x81);
    if ((((char)bVar16 < '\0') || (bVar16 < 0x66)) && (bVar16 < 0xf0)) {
      *(undefined1 *)(param_1 + 0x81) = 0x66;
    }
    bVar16 = *(byte *)(param_1 + 0x82);
    if ((((char)bVar16 < '\0') || (bVar16 < 0x66)) && (bVar16 < 0xf0)) {
      *(undefined1 *)(param_1 + 0x82) = 0x66;
    }
    if (0x1f < *(byte *)(param_1 + 0x83)) {
      iVar7 = _opd_FUN_008d2b58(*(undefined1 *)(param_1 + 0x7c),0,*pbVar1);
      uVar10 = 0;
      if (iVar7 != 0) {
        uVar10 = _opd_FUN_008d2708(*(undefined1 *)(param_1 + 0x7c),iVar7,*pbVar1);
      }
      *(undefined1 *)(param_1 + 0x83) = uVar10;
    }
    if (0x1f < *(byte *)(param_1 + 0x84)) {
      iVar7 = _opd_FUN_008d2b58(*(undefined1 *)(param_1 + 0x7d),0,*pbVar1);
      uVar10 = 0;
      if (iVar7 != 0) {
        uVar10 = _opd_FUN_008d2708(*(undefined1 *)(param_1 + 0x7d),iVar7,*pbVar1);
      }
      *(undefined1 *)(param_1 + 0x84) = uVar10;
    }
    if (0x1f < *(byte *)(param_1 + 0x86)) {
      iVar7 = _opd_FUN_008d2b58(*(undefined1 *)(param_1 + 0x7f),0,*pbVar1);
      uVar10 = 0;
      if (iVar7 != 0) {
        uVar10 = _opd_FUN_008d2708(*(undefined1 *)(param_1 + 0x7f),iVar7,*pbVar1);
      }
      *(undefined1 *)(param_1 + 0x86) = uVar10;
    }
    if (0x1f < *(byte *)(param_1 + 0x85)) {
      iVar7 = _opd_FUN_008d2b58(*(undefined1 *)(param_1 + 0x7e),0,*pbVar1);
      uVar10 = 0;
      if (iVar7 != 0) {
        uVar10 = _opd_FUN_008d2708(*(undefined1 *)(param_1 + 0x7e),iVar7,*pbVar1);
      }
      *(undefined1 *)(param_1 + 0x85) = uVar10;
    }
    if (0x1f < *(byte *)(param_1 + 0x87)) {
      iVar7 = _opd_FUN_008d2b58(*(undefined1 *)(param_1 + 0x80),0,*pbVar1);
      uVar10 = 0;
      if (iVar7 != 0) {
        uVar10 = _opd_FUN_008d2708(*(undefined1 *)(param_1 + 0x80),iVar7,*pbVar1);
      }
      *(undefined1 *)(param_1 + 0x87) = uVar10;
    }
    if (0x8a < *(byte *)(param_1 + 0x88)) {
      iVar7 = _opd_FUN_008d2b58(*(undefined1 *)(param_1 + 0x81),0,*pbVar1);
      uVar10 = 0;
      if (iVar7 != 0) {
        uVar10 = _opd_FUN_008d2708(*(undefined1 *)(param_1 + 0x81),iVar7,*pbVar1);
      }
      *(undefined1 *)(param_1 + 0x88) = uVar10;
    }
    if (0x8a < *(byte *)(param_1 + 0x89)) {
      iVar7 = _opd_FUN_008d2b58(*(undefined1 *)(param_1 + 0x82),0,*pbVar1);
      uVar10 = 0;
      if (iVar7 != 0) {
        uVar10 = _opd_FUN_008d2708(*(undefined1 *)(param_1 + 0x82),iVar7,*pbVar1);
      }
      *(undefined1 *)(param_1 + 0x89) = uVar10;
    }
    if (*(char *)(param_1 + 0x6a) == '\0') {
      puVar13 = *(undefined4 **)(param_1 + 0x138);
      uVar12 = 0;
      goto LAB_00a88420;
    }
    iVar7 = _opd_FUN_00f045d0(*(undefined4 *)(param_1 + 0x60));
    if (iVar7 == 0) {
LAB_00a87f94:
      uVar5 = 1;
      goto LAB_00a87f98;
    }
    if (iVar9 != 0) {
      pbVar17 = (byte *)(iVar9 + 0x3b08);
      lVar19 = 0x80;
      do {
        bVar2 = true;
        if (*pbVar17 < 0x1a) {
          bVar2 = 0x6000 < *(int *)(pbVar17 + 4);
        }
        if (bVar2) {
          *pbVar17 = 0;
          pbVar17[4] = 0;
          pbVar17[5] = 0;
          pbVar17[6] = 0;
          pbVar17[7] = 0;
        }
        pbVar17 = pbVar17 + 0xc;
        lVar19 = lVar19 + -1;
      } while (lVar19 != 0);
    }
    pbVar17 = (byte *)(param_1 + 0x8a);
    pbVar18 = (byte *)(param_1 + 0x8f);
    bVar16 = 0;
    iVar7 = 0;
    do {
      if ((*pbVar17 < 0x1a) && (*pbVar18 < 4)) {
        cVar11 = _opd_FUN_00a03dc8(*pbVar17,*pbVar18);
        bVar16 = cVar11 + bVar16;
        if (4 < bVar16) goto LAB_00a880b0;
        if (iVar9 != 0) {
          pbVar15 = (byte *)(iVar9 + 0x3b08);
          iVar8 = 0x3b00;
          lVar19 = 0x80;
          do {
            bVar3 = *pbVar15;
            pbVar15 = pbVar15 + 0xc;
            if (*pbVar17 == bVar3) {
              bVar3 = *pbVar18;
              iVar8 = _opd_FUN_0075ccf0(*(undefined2 *)(iVar9 + iVar8 + 0xe));
              if ((int)(uint)bVar3 <= iVar8) goto LAB_00a880b8;
              break;
            }
            iVar8 = iVar8 + 0xc;
            lVar19 = lVar19 + -1;
          } while (lVar19 != 0);
          goto LAB_00a880b0;
        }
LAB_00a880b8:
        bVar2 = false;
      }
      else {
LAB_00a880b0:
        bVar2 = true;
      }
      if (bVar2) {
        *pbVar18 = 0;
        *pbVar17 = 0;
        pbVar15 = pbVar1 + iVar7 + 0x28;
        pbVar15[0] = 0;
        pbVar15[1] = 0;
        pbVar15[2] = 0;
        pbVar15[3] = 0;
      }
      bVar2 = iVar7 != 0x10;
      pbVar17 = pbVar17 + 1;
      pbVar18 = pbVar18 + 1;
      iVar7 = iVar7 + 4;
    } while (bVar2);
    iVar7 = 0;
    do {
      pcVar14 = (char *)(param_1 + 0x8a);
      lVar19 = 5;
      puVar13 = (undefined4 *)(param_1 + 0x94);
      iVar8 = 0;
      cVar11 = *(char *)(param_1 + iVar7 + 0x8a);
      do {
        if ((iVar8 != iVar7) && (cVar11 == *pcVar14)) {
          *(undefined1 *)(param_1 + iVar8 + 0x8f) = 0;
          *pcVar14 = '\0';
          *puVar13 = 0;
        }
        iVar8 = iVar8 + 1;
        pcVar14 = pcVar14 + 1;
        puVar13 = puVar13 + 1;
        lVar19 = lVar19 + -1;
      } while (lVar19 != 0);
      bVar2 = iVar7 != 4;
      iVar7 = iVar7 + 1;
    } while (bVar2);
    if (iVar9 != 0) {
      pbVar17 = (byte *)(iVar9 + 0x2640);
      if (((((((*pbVar17 == *pbVar1) && (*(char *)(iVar9 + 0x2641) == *(char *)(param_1 + 0x6d))) &&
             (*(char *)(iVar9 + 0x2642) == *(char *)(param_1 + 0x6e))) &&
            ((*(char *)(iVar9 + 0x2643) == *(char *)(param_1 + 0x6f) &&
             (*(char *)(iVar9 + 0x2644) == *(char *)(param_1 + 0x70))))) &&
           (*(char *)(iVar9 + 0x2645) == *(char *)(param_1 + 0x71))) &&
          (((((*(char *)(iVar9 + 0x2646) == *(char *)(param_1 + 0x72) &&
              (*(char *)(iVar9 + 0x2647) == *(char *)(param_1 + 0x73))) &&
             ((*(char *)(iVar9 + 0x2648) == *(char *)(param_1 + 0x74) &&
              (((*(char *)(iVar9 + 0x2650) == *(char *)(param_1 + 0x7c) &&
                (*(char *)(iVar9 + 0x2651) == *(char *)(param_1 + 0x7d))) &&
               (*(char *)(iVar9 + 0x2652) == *(char *)(param_1 + 0x7e))))))) &&
            ((*(char *)(iVar9 + 0x2653) == *(char *)(param_1 + 0x7f) &&
             (*(char *)(iVar9 + 0x2654) == *(char *)(param_1 + 0x80))))) &&
           (*(char *)(iVar9 + 0x2655) == *(char *)(param_1 + 0x81))))) &&
         ((((*(char *)(iVar9 + 0x2656) == *(char *)(param_1 + 0x82) &&
            (*(char *)(iVar9 + 0x2657) == *(char *)(param_1 + 0x83))) &&
           ((*(char *)(iVar9 + 0x2658) == *(char *)(param_1 + 0x84) &&
            (((*(char *)(iVar9 + 0x2659) == *(char *)(param_1 + 0x85) &&
              (*(char *)(iVar9 + 0x265a) == *(char *)(param_1 + 0x86))) &&
             (*(char *)(iVar9 + 0x265b) == *(char *)(param_1 + 0x87))))))) &&
          ((*(char *)(iVar9 + 0x265c) == *(char *)(param_1 + 0x88) &&
           (*(char *)(iVar9 + 0x265d) == *(char *)(param_1 + 0x89))))))) {
        lVar19 = 5;
        iVar9 = 0;
        iVar7 = 0;
        do {
          if (((pbVar17[iVar9 + 0x1e] != pbVar1[iVar9 + 0x1e]) ||
              (pbVar17[iVar9 + 0x23] != pbVar1[iVar9 + 0x23])) ||
             (*(int *)(pbVar17 + iVar7 + 0x28) != *(int *)(pbVar1 + iVar7 + 0x28)))
          goto LAB_00a88360;
          lVar19 = lVar19 + -1;
          iVar9 = iVar9 + 1;
          iVar7 = iVar7 + 4;
        } while (lVar19 != 0);
        bVar2 = false;
      }
      else {
LAB_00a88360:
        bVar2 = true;
      }
      if (bVar2) goto LAB_00a87f94;
    }
  }
LAB_00a88424:
  uVar5 = 3;
LAB_00a87f98:
  *(undefined2 *)(param_1 + 0x68) = uVar5;
  return;
}


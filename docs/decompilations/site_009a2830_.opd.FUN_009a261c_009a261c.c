// hook SITE 0x009a2830

void _opd_FUN_009a261c(int param_1)

{
  bool bVar1;
  int *piVar2;
  undefined *puVar3;
  byte bVar5;
  undefined2 uVar4;
  int iVar6;
  undefined4 uVar7;
  short sVar10;
  char cVar11;
  int iVar8;
  uint uVar9;
  undefined8 uVar12;
  uint uVar13;
  ulonglong uVar14;
  int iVar15;
  char *pcVar16;
  char *pcVar17;
  byte *pbVar18;
  longlong lVar19;
  undefined4 local_830;
  undefined1 local_82c [12];
  undefined1 local_820 [16];
  undefined4 local_810;
  undefined1 auStack_80c [988];
  undefined1 auStack_430 [768];
  char acStack_130 [224];
  
  puVar3 = PTR_PTR_0121bb04;
  uVar14 = *(ulonglong *)(param_1 + 0x738);
  if ((uVar14 != 0) && (*(ulonglong *)((int)((uVar14 & 0xffffffff) << 4) + 0x20) == uVar14)) {
    return;
  }
  if (*(int *)(param_1 + 0x740) != 1) {
    piVar2 = *(int **)(PTR_PTR_0121bb04 + -0x8000);
    if (*(int *)(param_1 + 0x740) == 2) {
      iVar6 = *piVar2;
      uVar4 = 0x11;
    }
    else {
      *(ulonglong *)(param_1 + 0x740) = *(ulonglong *)(param_1 + 0x740) & 0xffffffff7fffffff;
      iVar6 = *piVar2;
      uVar4 = 0xd;
    }
    goto LAB_009a318c;
  }
  iVar6 = _opd_FUN_00280418();
  if (iVar6 != 0) {
    if (*(char *)(param_1 + 0x70) == '\0') {
      uVar7 = _opd_FUN_0027e2b0(0x19);
      _opd_FUN_0027e480(uVar7,0x8c,0x10,(char *)(param_1 + 0x70));
    }
    if (0xb < *(byte *)(param_1 + 0x3ba)) {
      *(undefined1 *)(param_1 + 0x3ba) = 0;
    }
    pcVar16 = (char *)(param_1 + 0x36c);
    pbVar18 = (byte *)(param_1 + 0x35c);
    pcVar17 = (char *)(param_1 + 0x37c);
    iVar15 = 0;
    do {
      bVar5 = *pbVar18;
      sVar10 = _opd_FUN_00a0c3ec(bVar5);
      if (sVar10 == -1) {
        *pbVar18 = 1;
      }
      if (((bVar5 < 0x11) && (bVar5 != 0xe)) && (bVar5 != 8)) {
        uVar12 = 7;
        if (((bVar5 != 0xc) && (uVar12 = 0xb, bVar5 != 0xf)) &&
           ((uVar12 = 10, bVar5 != 6 && ((uVar12 = 0xc, bVar5 != 0x10 && (uVar12 = 0, bVar5 != 7))))
           )) {
          if (bVar5 != 0xd) goto LAB_009a27cc;
          uVar12 = 8;
        }
        cVar11 = _opd_FUN_00f04238(iVar6,uVar12);
        if (cVar11 == '\0') {
          *pbVar18 = 1;
        }
      }
      else {
        *pbVar18 = 1;
      }
LAB_009a27cc:
      if ((*pcVar16 != '\0') && (sVar10 = _opd_FUN_00a0cd94(*pbVar18,*pcVar16), sVar10 == -1)) {
        cVar11 = _opd_FUN_00a0ce98(*pbVar18,0);
        *pcVar16 = cVar11;
      }
      if (*pcVar17 != '\0') {
        cVar11 = _opd_FUN_00a0ccb8(bVar5,*pcVar17);
        if (cVar11 == '\0') {
          *pcVar17 = '\0';
        }
        else if ((*pcVar17 == '\x04') &&
                ((cVar11 = _opd_FUN_00f04238(iVar6,0), cVar11 == '\0' ||
                 (iVar8 = _opd_FUN_0097d170(), iVar8 != 0)))) {
          *pcVar17 = '\0';
        }
      }
      bVar1 = iVar15 != 0xf;
      pbVar18 = pbVar18 + 1;
      pcVar17 = pcVar17 + 1;
      pcVar16 = pcVar16 + 1;
      iVar15 = iVar15 + 1;
    } while (bVar1);
    if (*(char *)(param_1 + 0x113) != '\0') {
      *(undefined1 *)(param_1 + 0x113) = 1;
    }
    bVar5 = *(byte *)(param_1 + 0x39e);
    if ((bVar5 < 2) || (0x10 < bVar5)) {
      *(undefined1 *)(param_1 + 0x39e) = 0x10;
    }
    else if ((bVar5 & 1) != 0) {
      *(byte *)(param_1 + 0x39e) = bVar5 - 1;
    }
    if (0x1d < *(int *)(param_1 + 0x3a0) - 1U) {
      *(undefined4 *)(param_1 + 0x3a0) = 2;
    }
    *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x40000;
    cVar11 = _opd_FUN_00f04238(iVar6,4);
    if ((cVar11 == '\0') || ((*(uint *)(param_1 + 0x40c) >> 0x15 & 1) != 0)) {
      *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x200000;
    }
    iVar6 = _opd_FUN_00280418();
    if (iVar6 != 0) {
      if ((*(uint *)(param_1 + 0x40c) >> 0x17 & 1) != 0) {
        *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x800000;
      }
      cVar11 = *(char *)(param_1 + 0x404);
      if (cVar11 < '\0') {
        iVar6 = _opd_FUN_0097cd48();
        if ((iVar6 != 0) && (-1 < (char)*(byte *)(param_1 + 0x405))) {
          bVar5 = *(byte *)(param_1 + 0x405) | 0x80;
LAB_009a2c80:
          *(byte *)(param_1 + 0x405) = bVar5;
        }
      }
      else {
        if (((4 < (byte)(cVar11 - 2U)) && (cVar11 != '\t')) &&
           ((cVar11 = _opd_FUN_00f04238(iVar6,0x11), cVar11 == '\0' ||
            (*(char *)(param_1 + 0x404) != '\0')))) {
          *(undefined1 *)(param_1 + 0x404) = 2;
        }
        if (((4 < (byte)(*(char *)(param_1 + 0x405) - 2U)) && (*(char *)(param_1 + 0x405) != '\t'))
           && ((cVar11 = _opd_FUN_00f04238(iVar6,0x11), cVar11 == '\0' ||
               (*(char *)(param_1 + 0x405) != '\0')))) {
          *(undefined1 *)(param_1 + 0x405) = 3;
        }
        if (*(char *)(param_1 + 0x404) == *(char *)(param_1 + 0x405)) {
          *(undefined1 *)(param_1 + 0x404) = 2;
          *(undefined1 *)(param_1 + 0x405) = 3;
        }
        iVar6 = _opd_FUN_0097cd48();
        if ((iVar6 != 0) &&
           ((cVar11 = *(char *)(param_1 + 0x404), cVar11 == '\0' ||
            (*(char *)(param_1 + 0x405) == '\0')))) {
          _opd_FUN_00f9ac38(auStack_80c,param_1 + 0x6c,0x3dc);
          _opd_FUN_00f9ac38(auStack_430,auStack_80c,0x3dc);
          lVar19 = 0x10;
          pcVar16 = acStack_130;
          do {
            if ((*pcVar16 != '\0') && (pcVar16[-0x10] == '\x04')) {
              uVar14 = 0;
              local_820 = (undefined1  [16])0x0;
              local_810 = 0;
              local_82c = SUB1612((undefined1  [16])0x0,0);
              local_830 = 0xffffffff;
              if (cVar11 == '\x02') {
LAB_009a2b3c:
                if (*(char *)(param_1 + 0x405) != '\x03') {
                  lVar19 = uVar14 << 2;
                  uVar14 = uVar14 + 1;
                  *(undefined4 *)(local_82c + (int)lVar19 + -4) = 3;
                }
                if (cVar11 != '\x04') goto LAB_009a2b68;
LAB_009a2b94:
                if (*(char *)(param_1 + 0x405) != '\x05') {
                  lVar19 = uVar14 << 2;
                  uVar14 = uVar14 + 1;
                  *(undefined4 *)(local_82c + (int)lVar19 + -4) = 5;
                }
                if (cVar11 != '\x06') goto LAB_009a2bc0;
LAB_009a2bec:
                uVar13 = (uint)uVar14;
                if (*(char *)(param_1 + 0x405) != '\t') {
                  uVar13 = uVar13 + 1;
                  *(undefined4 *)(local_82c + (int)(uVar14 << 2) + -4) = 9;
                }
              }
              else {
                bVar1 = *(char *)(param_1 + 0x405) != '\x02';
                if (bVar1) {
                  local_830 = 2;
                }
                uVar14 = (ulonglong)bVar1;
                if (cVar11 != '\x03') goto LAB_009a2b3c;
LAB_009a2b68:
                if (*(char *)(param_1 + 0x405) != '\x04') {
                  lVar19 = uVar14 << 2;
                  uVar14 = uVar14 + 1;
                  *(undefined4 *)(local_82c + (int)lVar19 + -4) = 4;
                }
                if (cVar11 != '\x05') goto LAB_009a2b94;
LAB_009a2bc0:
                if (*(char *)(param_1 + 0x405) != '\x06') {
                  lVar19 = uVar14 << 2;
                  uVar14 = uVar14 + 1;
                  *(undefined4 *)(local_82c + (int)lVar19 + -4) = 6;
                }
                uVar13 = (uint)uVar14;
                if (cVar11 != '\t') goto LAB_009a2bec;
              }
              if (uVar13 != 0) {
                piVar2 = *(int **)(puVar3 + -0x7ffc);
                uVar14 = (longlong)*piVar2 * 0x5d588b65 + 1;
                *piVar2 = (int)uVar14;
                uVar14 = uVar14 - (longlong)(int)((uVar14 & 0xffffffff) / (ulonglong)uVar13) *
                                  (longlong)(int)uVar13;
                if (*(char *)(param_1 + 0x404) == '\0') {
                  *(char *)(param_1 + 0x404) =
                       (char)*(undefined4 *)(local_82c + (int)((uVar14 & 0xffffffff) << 2) + -4);
                }
                else if (*(char *)(param_1 + 0x405) == '\0') {
                  bVar5 = (byte)*(undefined4 *)(local_82c + (int)((uVar14 & 0xffffffff) << 2) + -4);
                  goto LAB_009a2c80;
                }
              }
              break;
            }
            lVar19 = lVar19 + -1;
            pcVar16 = pcVar16 + 1;
          } while (lVar19 != 0);
        }
      }
    }
    if ((*(uint *)(param_1 + 0x40c) >> 0x14 & 1) != 0) {
      *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x100000;
    }
    if ((*(uint *)(param_1 + 0x40c) >> 0x13 & 1) != 0) {
      *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x80000;
    }
    if ((*(uint *)(param_1 + 0x40c) >> 0x10 & 1) == 0) {
LAB_009a2cd4:
      *(undefined2 *)(param_1 + 0x410) = 5;
    }
    else {
      *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x10000;
      if ((*(ushort *)(param_1 + 0x410) == 0) || (99 < *(ushort *)(param_1 + 0x410)))
      goto LAB_009a2cd4;
    }
    if ((*(uint *)(param_1 + 0x40c) >> 0xf & 1) == 0) {
LAB_009a2d04:
      *(undefined2 *)(param_1 + 0x412) = 3;
    }
    else {
      *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x8000;
      if ((*(ushort *)(param_1 + 0x412) == 0) || (99 < *(ushort *)(param_1 + 0x412)))
      goto LAB_009a2d04;
    }
    if ((*(uint *)(param_1 + 0x40c) >> 0xe & 1) != 0) {
      *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x4000;
    }
    if ((*(uint *)(param_1 + 0x40c) >> 0xd & 1) != 0) {
      *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x2000;
    }
    uVar9 = _opd_FUN_0075e000();
    uVar13 = *(uint *)(param_1 + 0x40c);
    if ((uVar13 & 0x1000) == 0) {
      *(char *)(param_1 + 0x3bb) = (char)uVar9;
      *(uint *)(param_1 + 0x3bc) = uVar13 & 0x1000;
    }
    else {
      bVar5 = *(byte *)(param_1 + 0x3bb);
      *(uint *)(param_1 + 0x40c) = uVar13 | 0x1000;
      if (bVar5 == 0) {
        *(byte *)(param_1 + 0x3bb) = bVar5;
      }
      else if (uVar9 < bVar5) {
        *(char *)(param_1 + 0x3bb) = (char)uVar9;
      }
      if (uVar9 < *(uint *)(param_1 + 0x3bc)) {
        *(uint *)(param_1 + 0x3bc) = uVar9;
      }
    }
    if ((*(uint *)(param_1 + 0x40c) >> 0xb & 1) != 0) {
      *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x800;
    }
    if ((*(uint *)(param_1 + 0x40c) >> 10 & 1) != 0) {
      *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x400;
    }
    if ((*(uint *)(param_1 + 0x40c) >> 9 & 1) != 0) {
      *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x200;
    }
    if ((*(uint *)(param_1 + 0x40c) >> 8 & 1) != 0) {
      *(uint *)(param_1 + 0x40c) = *(uint *)(param_1 + 0x40c) | 0x100;
    }
    if ((*(uint *)(param_1 + 0x3e4) < 2) || (99 < *(uint *)(param_1 + 0x3e4))) {
      *(undefined4 *)(param_1 + 0x3e4) = 5;
    }
    if ((*(byte *)(param_1 + 0x41d) == 0) || (0x14 < *(byte *)(param_1 + 0x41d))) {
      *(undefined1 *)(param_1 + 0x41d) = 1;
    }
    if ((*(uint *)(param_1 + 1000) == 0) || (99 < *(uint *)(param_1 + 1000))) {
      *(undefined4 *)(param_1 + 1000) = 0x1e;
    }
    if ((*(byte *)(param_1 + 0x41a) < 3) || (99 < *(byte *)(param_1 + 0x41a))) {
      *(undefined1 *)(param_1 + 0x41a) = 5;
    }
    if ((*(byte *)(param_1 + 0x41b) == 0) || (0x14 < *(byte *)(param_1 + 0x41b))) {
      *(undefined1 *)(param_1 + 0x41b) = 2;
    }
    if ((*(byte *)(param_1 + 0x41e) < 2) || (99 < *(byte *)(param_1 + 0x41e))) {
      *(undefined1 *)(param_1 + 0x41e) = 5;
    }
    if ((*(byte *)(param_1 + 0x41f) == 0) || (0x14 < *(byte *)(param_1 + 0x41f))) {
      *(undefined1 *)(param_1 + 0x41f) = 2;
    }
    if ((*(uint *)(param_1 + 0x3d8) < 2) || (99 < *(uint *)(param_1 + 0x3d8))) {
      *(undefined4 *)(param_1 + 0x3d8) = 5;
    }
    uVar13 = *(uint *)(param_1 + 0x3dc);
    if ((uVar13 < 2) || (0x14 < uVar13)) {
      iVar6 = 2;
LAB_009a2ee4:
      *(int *)(param_1 + 0x3dc) = iVar6;
    }
    else if ((uVar13 & 1) != 0) {
      iVar6 = uVar13 - 1;
      goto LAB_009a2ee4;
    }
    if ((*(uint *)(param_1 + 0x3e0) == 0) || (99 < *(uint *)(param_1 + 0x3e0))) {
      *(undefined4 *)(param_1 + 0x3e0) = 0x32;
    }
    if ((*(uint *)(param_1 + 0x3c8) < 2) || (99 < *(uint *)(param_1 + 0x3c8))) {
      *(undefined4 *)(param_1 + 0x3c8) = 4;
    }
    uVar13 = *(uint *)(param_1 + 0x3cc);
    if ((uVar13 < 2) || (0x14 < uVar13)) {
      iVar6 = 2;
LAB_009a2f48:
      *(int *)(param_1 + 0x3cc) = iVar6;
    }
    else if ((uVar13 & 1) != 0) {
      iVar6 = uVar13 - 1;
      goto LAB_009a2f48;
    }
    if (1 < *(byte *)(param_1 + 0x418)) {
      *(undefined1 *)(param_1 + 0x419) = 1;
    }
    if ((*(uint *)(param_1 + 0x3ec) < 2) || (99 < *(uint *)(param_1 + 0x3ec))) {
      *(undefined4 *)(param_1 + 0x3ec) = 5;
    }
    uVar13 = *(uint *)(param_1 + 0x3f0);
    if ((uVar13 < 2) || (0x14 < uVar13)) {
      iVar6 = 2;
LAB_009a2fa4:
      *(int *)(param_1 + 0x3f0) = iVar6;
    }
    else if ((uVar13 & 1) != 0) {
      iVar6 = uVar13 - 1;
      goto LAB_009a2fa4;
    }
    if ((*(uint *)(param_1 + 0x3f4) < 2) || (99 < *(uint *)(param_1 + 0x3f4))) {
      *(undefined4 *)(param_1 + 0x3f4) = 7;
    }
    uVar13 = *(uint *)(param_1 + 0x3f8);
    if ((uVar13 < 2) || (0x14 < uVar13)) {
      iVar6 = 2;
LAB_009a2fec:
      *(int *)(param_1 + 0x3f8) = iVar6;
    }
    else if ((uVar13 & 1) != 0) {
      iVar6 = uVar13 - 1;
      goto LAB_009a2fec;
    }
    if ((*(byte *)(param_1 + 0x420) < 2) || (99 < *(byte *)(param_1 + 0x420))) {
      *(undefined1 *)(param_1 + 0x420) = 5;
    }
    bVar5 = *(byte *)(param_1 + 0x421);
    if ((bVar5 < 2) || (0x14 < bVar5)) {
      cVar11 = '\x02';
LAB_009a3038:
      *(char *)(param_1 + 0x421) = cVar11;
    }
    else if ((bVar5 & 1) != 0) {
      cVar11 = bVar5 - 1;
      goto LAB_009a3038;
    }
    if ((*(uint *)(param_1 + 0x3d0) < 2) || (99 < *(uint *)(param_1 + 0x3d0))) {
      *(undefined4 *)(param_1 + 0x3d0) = 7;
    }
    uVar13 = *(uint *)(param_1 + 0x3d4);
    if ((uVar13 < 2) || (0x14 < uVar13)) {
      iVar6 = 2;
LAB_009a3080:
      *(int *)(param_1 + 0x3d4) = iVar6;
    }
    else if ((uVar13 & 1) != 0) {
      iVar6 = uVar13 - 1;
      goto LAB_009a3080;
    }
    if ((*(uint *)(param_1 + 0x3fc) < 2) || (99 < *(uint *)(param_1 + 0x3fc))) {
      *(undefined4 *)(param_1 + 0x3fc) = 10;
    }
    uVar13 = *(uint *)(param_1 + 0x400);
    if ((uVar13 < 2) || (0x14 < uVar13)) {
      iVar6 = 2;
LAB_009a30c8:
      *(int *)(param_1 + 0x400) = iVar6;
    }
    else if ((uVar13 & 1) != 0) {
      iVar6 = uVar13 - 1;
      goto LAB_009a30c8;
    }
    if ((*(uint *)(param_1 + 0x3c0) < 4) || (99 < *(uint *)(param_1 + 0x3c0))) {
      *(undefined4 *)(param_1 + 0x3c0) = 7;
    }
    uVar13 = *(uint *)(param_1 + 0x3c4);
    if ((uVar13 < 2) || (0x14 < uVar13)) {
      iVar6 = 2;
LAB_009a3110:
      *(int *)(param_1 + 0x3c4) = iVar6;
    }
    else if ((uVar13 & 1) != 0) {
      iVar6 = uVar13 - 1;
      goto LAB_009a3110;
    }
    if ((*(byte *)(param_1 + 0x419) == 0) || (5 < *(byte *)(param_1 + 0x419))) {
      *(undefined1 *)(param_1 + 0x419) = 3;
    }
    if ((*(byte *)(param_1 + 0x41c) < 2) || (99 < *(byte *)(param_1 + 0x41c))) {
      *(undefined1 *)(param_1 + 0x41c) = 3;
    }
  }
  uVar4 = 0x13;
  iVar6 = **(int **)(puVar3 + -0x8000);
LAB_009a318c:
  *(undefined2 *)(iVar6 + 100) = uVar4;
  return;
}


/* containing function of static patch target(s); entry .opd.FUN_00c2e5c0 @ 00c2e5c0 */

void _opd_FUN_00c2e5c0(int param_1)

{
  undefined1 uVar1;
  undefined1 uVar2;
  undefined1 uVar3;
  short sVar4;
  undefined4 *puVar5;
  undefined1 *puVar6;
  undefined *puVar7;
  short sVar8;
  int iVar9;
  undefined4 uVar10;
  int *piVar11;
  int iVar12;
  int iVar13;
  int iVar14;
  undefined1 *puVar15;
  uint uVar16;
  uint local_50 [4];
  
  puVar7 = PTR_DAT_0121be74;
  if (*(int *)(param_1 + 0x7c) != 0) {
    _opd_FUN_0097f1b8(0x19e0,*(int *)(param_1 + 0x7c),0);
    *(undefined4 *)(param_1 + 0x7c) = 0;
    return;
  }
  if (*(int *)(param_1 + 0x70) != 1) {
    if (*(int *)(param_1 + 0x70) != 2) {
      return;
    }
    *(int *)(param_1 + 0x188) =
         *(int *)(param_1 + 0x188) - *(int *)(*(int *)(PTR_DAT_0121be74 + -0x7fd0) + 0xc);
    if (*(short *)(param_1 + 0x78) == 1) {
      piVar11 = *(int **)(param_1 + 100);
      puVar5 = *(undefined4 **)(*piVar11 + 0x20);
    }
    else {
      piVar11 = *(int **)(param_1 + 0x60);
      puVar5 = *(undefined4 **)(*piVar11 + 8);
    }
    iVar9 = (*(code *)*puVar5)(piVar11,param_1 + 0x184);
    if (iVar9 == 1) {
      return;
    }
    if (iVar9 == 2) {
      iVar9 = *(int *)(puVar7 + -0x8000);
      if (*(char *)(iVar9 + 4) == '\0') {
        sVar8 = *(short *)(param_1 + 0x78);
        *(uint *)(param_1 + 0x7c) =
             ((int)~*(uint *)(param_1 + 0x188) >> 0x1f & 0xfffd0000U) + 0x40000 | (int)sVar8;
      }
      else {
        sVar8 = *(short *)(param_1 + 0x78);
      }
      if (sVar8 == 1) {
        *(undefined1 *)(iVar9 + 5) = 0;
      }
      *(undefined1 *)(iVar9 + 4) = 1;
      goto LAB_00c2e6d4;
    }
    sVar8 = *(short *)(param_1 + 0x78);
    if (sVar8 == 2) {
      iVar9 = (*(code *)**(undefined4 **)(**(int **)(param_1 + 0x60) + 0xc))
                        (*(int **)(param_1 + 0x60));
      if (iVar9 < 1) goto LAB_00c2efc8;
      _opd_FUN_002892a8(iVar9);
      iVar9 = *(int *)(puVar7 + -0x8000);
      _opd_FUN_00f9ac38(iVar9 + 0x1288,*(short *)(param_1 + 0x76) * 0x94 + iVar9 + 0x1c,0x80);
    }
    else {
      if (sVar8 == 3) {
        *(undefined2 *)(param_1 + 0x20c) = 0;
        iVar9 = (*(code *)**(undefined4 **)(**(int **)(param_1 + 0x60) + 0xc))
                          (*(int **)(param_1 + 0x60));
        if (0 < iVar9) {
          iVar9 = *(int *)(param_1 + 0x210);
          if (iVar9 == 0) {
            iVar9 = _opd_FUN_000bc1b0(0xffffffffffffffff,0x100000,0);
            *(int *)(param_1 + 0x210) = iVar9;
            if (iVar9 == 0) goto LAB_00c2efc8;
          }
          _opd_FUN_000d60b8(iVar9,0x100000);
          iVar12 = 0xff800;
          iVar9 = _opd_FUN_00c24388(*(undefined4 *)(param_1 + 0x214),
                                    *(undefined4 *)(param_1 + 0x210),0x200,0x200);
          do {
            _opd_FUN_00f9ac38(*(undefined4 *)(param_1 + 0x214),
                              (0xff800 - iVar12) + *(int *)(param_1 + 0x210),0x800);
            _opd_FUN_00f9ac38(*(int *)(param_1 + 0x210) + (0xff800 - iVar12),
                              iVar12 + *(int *)(param_1 + 0x210),0x800);
            iVar14 = iVar12 + *(int *)(param_1 + 0x210);
            iVar12 = iVar12 + -0x800;
            _opd_FUN_00f9ac38(iVar14,*(undefined4 *)(param_1 + 0x214),0x800);
          } while (iVar12 != 0x7f800);
          if (-1 < iVar9) {
            iVar9 = *(int *)(puVar7 + -0x8000);
            *(undefined2 *)(param_1 + 0x20c) =
                 *(undefined2 *)(*(short *)(param_1 + 0x76) * 0x94 + iVar9 + 0xc);
            _opd_FUN_00f9ac38(param_1 + 0x18c,*(short *)(param_1 + 0x76) * 0x94 + iVar9 + 0x1c,0x80)
            ;
            goto LAB_00c2e6cc;
          }
        }
LAB_00c2efc8:
        iVar9 = *(int *)(puVar7 + -0x8000);
LAB_00c2ecac:
        if (*(char *)(iVar9 + 4) == '\0') {
          sVar8 = *(short *)(param_1 + 0x78);
          *(uint *)(param_1 + 0x7c) = (int)sVar8 | 0x20000;
        }
        else {
          sVar8 = *(short *)(param_1 + 0x78);
        }
        if (sVar8 == 1) {
          *(undefined1 *)(iVar9 + 5) = 0;
        }
        *(undefined1 *)(iVar9 + 4) = 1;
        goto LAB_00c2e6d4;
      }
      if (sVar8 != 1) {
        iVar9 = *(int *)(puVar7 + -0x8000);
        goto LAB_00c2e6cc;
      }
      iVar9 = *(int *)(puVar7 + -0x8000);
      _opd_FUN_000d60b8(iVar9 + 8,0x1280);
      _opd_FUN_00d81a18(*(undefined4 *)(param_1 + 0x80),*(undefined4 *)(param_1 + 0x184));
      iVar12 = _opd_FUN_00d827c8(local_50);
      if (iVar12 == 0) {
        *(undefined1 *)(iVar9 + 5) = 0;
        goto LAB_00c2ecac;
      }
      if (local_50[0] < 0x21) {
        if (local_50[0] != 0) goto LAB_00c2ebc8;
      }
      else {
        local_50[0] = 0x20;
LAB_00c2ebc8:
        iVar12 = iVar9 + 8;
        uVar16 = 0;
        do {
          uVar16 = uVar16 + 1;
          iVar14 = _opd_FUN_00d827c8(iVar12);
          if (iVar14 == 0) goto LAB_00c2ecac;
          iVar14 = _opd_FUN_00d82760(iVar12 + 4);
          if (iVar14 == 0) goto LAB_00c2ecac;
          iVar14 = _opd_FUN_00d82700(iVar12 + 6);
          if (iVar14 == 0) goto LAB_00c2ecac;
          iVar14 = _opd_FUN_00d82700(iVar12 + 7);
          if (iVar14 == 0) goto LAB_00c2ecac;
          iVar14 = _opd_FUN_00d82700(iVar12 + 8);
          if (iVar14 == 0) goto LAB_00c2ecac;
          iVar14 = _opd_FUN_00d827c8(iVar12 + 0xc);
          if (iVar14 == 0) goto LAB_00c2ecac;
          iVar13 = _opd_FUN_00d827c8(iVar12 + 0x10);
          iVar14 = iVar12 + 0x14;
          iVar12 = iVar12 + 0x94;
          if ((iVar13 == 0) || (iVar14 = _opd_FUN_00d82848(iVar14,0x80), iVar14 == 0))
          goto LAB_00c2ecac;
        } while (uVar16 < local_50[0]);
      }
      *(undefined2 *)(param_1 + 0x74) = 1;
      *(undefined1 *)(iVar9 + 5) = 1;
    }
LAB_00c2e6cc:
    *(undefined1 *)(iVar9 + 4) = 0;
LAB_00c2e6d4:
    if (*(int *)(param_1 + 0x214) != 0) {
      _opd_FUN_000bd0b8(*(int *)(param_1 + 0x214));
      *(undefined4 *)(param_1 + 0x214) = 0;
    }
    piVar11 = *(int **)(param_1 + 0x60);
    if (piVar11 != (int *)0x0) {
      (*(code *)**(undefined4 **)(*piVar11 + 4))(piVar11);
      *(undefined4 *)(param_1 + 0x60) = 0;
    }
    piVar11 = *(int **)(param_1 + 100);
    if (piVar11 != (int *)0x0) {
      (*(code *)**(undefined4 **)(*piVar11 + 4))(piVar11);
      *(undefined4 *)(param_1 + 100) = 0;
    }
    *(undefined4 *)(param_1 + 0x70) = 1;
    return;
  }
  if ((*(short *)(param_1 + 0x78) == 1) &&
     (*(char *)(*(int *)(PTR_DAT_0121be74 + -0x8000) + 5) == '\0')) {
LAB_00c2ea6c:
    *(undefined2 *)(param_1 + 0x74) = 0;
    *(undefined4 *)(param_1 + 0x70) = 0;
    return;
  }
  sVar8 = *(short *)(param_1 + 0x76);
  if (sVar8 == -1) {
    if (*(char *)(*(int *)(PTR_DAT_0121be74 + -0x8000) + 5) != '\0') {
      *(undefined2 *)(param_1 + 0x76) = 0;
      goto LAB_00c2e7d8;
    }
  }
  else {
    *(short *)(param_1 + 0x76) = sVar8 + 1;
    if (0x1f < (short)(sVar8 + 1)) goto LAB_00c2ea6c;
LAB_00c2e7d8:
    sVar8 = sVar8 + 1;
    if (sVar8 != -1) {
      iVar9 = *(int *)(puVar7 + -0x8000);
      do {
        iVar12 = (int)sVar8;
        sVar8 = *(short *)(param_1 + 0x76) + 1;
        iVar14 = *(short *)(param_1 + 0x76) * 0x94 + iVar9;
        *(ushort *)(param_1 + 0x78) = (ushort)*(byte *)(iVar12 * 0x94 + iVar9 + 0xe);
        if ((((*(char *)(iVar14 + 0x10) != '\0') && (*(short *)(param_1 + 0x74) != 0)) ||
            (sVar4 = *(short *)(iVar14 + 0xc), *(short *)(param_1 + 0x7a) == sVar4)) || (sVar4 == 0)
           ) goto LAB_00c2e85c;
        *(short *)(param_1 + 0x76) = sVar8;
        if (0x1f < sVar8) goto LAB_00c2ea6c;
      } while (sVar8 != -1);
    }
  }
  *(undefined2 *)(param_1 + 0x78) = 1;
LAB_00c2e85c:
  sVar8 = *(short *)(param_1 + 0x78);
  if (sVar8 == 2) {
    iVar9 = *(int *)(puVar7 + -0x8000);
    if ((*(char *)(iVar9 + 0x1288) != '\0') &&
       (iVar12 = _opd_FUN_00f9db58(iVar9 + 0x1288,*(short *)(param_1 + 0x76) * 0x94 + iVar9 + 0x1c),
       iVar12 == 0)) {
      iVar12 = _opd_FUN_002892c0();
      if (iVar12 != 0) {
        return;
      }
      sVar8 = *(short *)(param_1 + 0x78);
      if (sVar8 != 2) {
        if (sVar8 == 3) goto LAB_00c2ee74;
        if (sVar8 != 1) goto LAB_00c2ed40;
        goto LAB_00c2e88c;
      }
    }
    iVar12 = param_1 + 0x84;
    _opd_FUN_002892a8(0);
    _opd_FUN_000d60b8(iVar9 + 0x1288,0x80);
    uVar10 = _opd_FUN_002891e0(2);
    _opd_FUN_00f9dca0(iVar12,uVar10);
    sVar8 = *(short *)(param_1 + 0x78);
    if (sVar8 == 1) {
      iVar9 = _opd_FUN_00f9dde8(iVar12);
      puVar6 = *(undefined1 **)(puVar7 + -0x7fe8);
      puVar15 = (undefined1 *)(iVar12 + iVar9);
      uVar1 = puVar6[1];
      uVar2 = puVar6[2];
      uVar3 = puVar6[3];
      *puVar15 = *puVar6;
      puVar15[1] = uVar1;
      puVar15[2] = uVar2;
      puVar15[3] = uVar3;
      uVar1 = puVar6[5];
      uVar2 = puVar6[6];
      uVar3 = puVar6[7];
      puVar15[4] = puVar6[4];
      puVar15[5] = uVar1;
      puVar15[6] = uVar2;
      puVar15[7] = uVar3;
      uVar1 = puVar6[9];
      uVar2 = puVar6[10];
      uVar3 = puVar6[0xb];
      puVar15[8] = puVar6[8];
      puVar15[9] = uVar1;
      puVar15[10] = uVar2;
      puVar15[0xb] = uVar3;
      uVar1 = puVar6[0xd];
      uVar2 = puVar6[0xe];
      uVar3 = puVar6[0xf];
      puVar15[0xc] = puVar6[0xc];
      puVar15[0xd] = uVar1;
      puVar15[0xe] = uVar2;
      puVar15[0xf] = uVar3;
      uVar1 = puVar6[0x12];
      uVar2 = puVar6[0x11];
      puVar15[0x10] = puVar6[0x10];
      puVar15[0x12] = uVar1;
      puVar15[0x11] = uVar2;
    }
    else if ((sVar8 < 1) || (3 < sVar8)) {
      iVar12 = 0;
    }
    else {
      _opd_FUN_00228b78(iVar12,*(undefined4 *)(puVar7 + -0x7fe4),
                        *(short *)(param_1 + 0x76) * 0x94 + iVar9 + 0x1c);
    }
    uVar10 = _opd_FUN_00289290();
    uVar10 = _opd_FUN_00d7d050(iVar12,uVar10,0x40000,0x1a);
    *(undefined4 *)(param_1 + 0x60) = uVar10;
  }
  else {
    if (sVar8 == 3) {
      if ((*(char *)(param_1 + 0x18c) != '\0') &&
         (iVar9 = _opd_FUN_00f9db58((char *)(param_1 + 0x18c),
                                    *(short *)(param_1 + 0x76) * 0x94 + *(int *)(puVar7 + -0x8000) +
                                    0x1c), iVar9 == 0)) {
        return;
      }
LAB_00c2ee74:
      iVar9 = *(int *)(param_1 + 0x214);
      *(undefined2 *)(param_1 + 0x20c) = 0;
      if (iVar9 == 0) {
        iVar9 = _opd_FUN_000bc1b0(0xffffffffffffffff,0x20000,0);
        *(int *)(param_1 + 0x214) = iVar9;
        if (iVar9 == 0) goto LAB_00c2ed40;
      }
      iVar12 = param_1 + 0x84;
      _opd_FUN_000d60b8(iVar9,0x20000);
      _opd_FUN_000d60b8(param_1 + 0x18c,0x80);
      uVar10 = _opd_FUN_002891e0(2);
      _opd_FUN_00f9dca0(iVar12,uVar10);
      sVar8 = *(short *)(param_1 + 0x78);
      if (sVar8 == 1) {
        iVar9 = _opd_FUN_00f9dde8(iVar12);
        puVar6 = *(undefined1 **)(puVar7 + -0x7fe8);
        puVar15 = (undefined1 *)(iVar12 + iVar9);
        uVar1 = puVar6[1];
        uVar2 = puVar6[2];
        uVar3 = puVar6[3];
        *puVar15 = *puVar6;
        puVar15[1] = uVar1;
        puVar15[2] = uVar2;
        puVar15[3] = uVar3;
        uVar1 = puVar6[5];
        uVar2 = puVar6[6];
        uVar3 = puVar6[7];
        puVar15[4] = puVar6[4];
        puVar15[5] = uVar1;
        puVar15[6] = uVar2;
        puVar15[7] = uVar3;
        uVar1 = puVar6[9];
        uVar2 = puVar6[10];
        uVar3 = puVar6[0xb];
        puVar15[8] = puVar6[8];
        puVar15[9] = uVar1;
        puVar15[10] = uVar2;
        puVar15[0xb] = uVar3;
        uVar1 = puVar6[0xd];
        uVar2 = puVar6[0xe];
        uVar3 = puVar6[0xf];
        puVar15[0xc] = puVar6[0xc];
        puVar15[0xd] = uVar1;
        puVar15[0xe] = uVar2;
        puVar15[0xf] = uVar3;
        uVar1 = puVar6[0x12];
        uVar2 = puVar6[0x11];
        puVar15[0x10] = puVar6[0x10];
        puVar15[0x12] = uVar1;
        puVar15[0x11] = uVar2;
      }
      else if ((sVar8 < 1) || (3 < sVar8)) {
        iVar12 = 0;
      }
      else {
        _opd_FUN_00228b78(iVar12,*(undefined4 *)(puVar7 + -0x7fe4),
                          *(short *)(param_1 + 0x76) * 0x94 + *(int *)(puVar7 + -0x8000) + 0x1c);
      }
      uVar10 = _opd_FUN_00d7d050(iVar12,*(undefined4 *)(param_1 + 0x214),0x20000,0x1a);
      *(undefined4 *)(param_1 + 0x60) = uVar10;
      goto LAB_00c2eb38;
    }
    if (sVar8 != 1) {
      return;
    }
    iVar9 = *(int *)(puVar7 + -0x8000);
    if (*(char *)(iVar9 + 5) != '\0') {
      return;
    }
LAB_00c2e88c:
    iVar12 = *(int *)(param_1 + 0x80);
    *(undefined4 *)(param_1 + 0x184) = 0;
    if (iVar12 == 0) {
      iVar12 = _opd_FUN_000bc1b0(0xffffffffffffffff,0xc800,0);
      *(int *)(param_1 + 0x80) = iVar12;
      if (iVar12 == 0) {
        *(undefined1 *)(iVar9 + 5) = 0;
LAB_00c2ed40:
        if (*(short *)(param_1 + 0x76) == -1) {
          *(undefined4 *)(param_1 + 0x70) = 0;
        }
        else {
          *(undefined4 *)(param_1 + 0x70) = 2;
        }
        return;
      }
    }
    iVar14 = param_1 + 0x84;
    _opd_FUN_000d60b8(iVar12,0xc800);
    uVar10 = _opd_FUN_002891e0(2);
    _opd_FUN_00f9dca0(iVar14,uVar10);
    sVar8 = *(short *)(param_1 + 0x78);
    if (sVar8 == 1) {
      iVar12 = _opd_FUN_00f9dde8(iVar14);
      puVar6 = *(undefined1 **)(puVar7 + -0x7fe8);
      puVar15 = (undefined1 *)(iVar14 + iVar12);
      uVar1 = puVar6[1];
      uVar2 = puVar6[2];
      uVar3 = puVar6[3];
      *puVar15 = *puVar6;
      puVar15[1] = uVar1;
      puVar15[2] = uVar2;
      puVar15[3] = uVar3;
      uVar1 = puVar6[5];
      uVar2 = puVar6[6];
      uVar3 = puVar6[7];
      puVar15[4] = puVar6[4];
      puVar15[5] = uVar1;
      puVar15[6] = uVar2;
      puVar15[7] = uVar3;
      uVar1 = puVar6[9];
      uVar2 = puVar6[10];
      uVar3 = puVar6[0xb];
      puVar15[8] = puVar6[8];
      puVar15[9] = uVar1;
      puVar15[10] = uVar2;
      puVar15[0xb] = uVar3;
      uVar1 = puVar6[0xd];
      uVar2 = puVar6[0xe];
      uVar3 = puVar6[0xf];
      puVar15[0xc] = puVar6[0xc];
      puVar15[0xd] = uVar1;
      puVar15[0xe] = uVar2;
      puVar15[0xf] = uVar3;
      uVar1 = puVar6[0x12];
      uVar2 = puVar6[0x11];
      puVar15[0x10] = puVar6[0x10];
      puVar15[0x12] = uVar1;
      puVar15[0x11] = uVar2;
    }
    else if ((sVar8 < 1) || (3 < sVar8)) {
      iVar14 = 0;
    }
    else {
      _opd_FUN_00228b78(iVar14,*(undefined4 *)(puVar7 + -0x7fe4),
                        *(short *)(param_1 + 0x76) * 0x94 + iVar9 + 0x1c);
    }
    piVar11 = (int *)_opd_FUN_00d828e8(iVar14,*(undefined4 *)(param_1 + 0x80),0xc800,5);
    uVar10 = *(undefined4 *)(puVar7 + -0x7fe0);
    *(int **)(param_1 + 100) = piVar11;
    (*(code *)**(undefined4 **)(*piVar11 + 0xc))(piVar11,uVar10,0);
    (*(code *)**(undefined4 **)(**(int **)(param_1 + 100) + 0xc))
              (*(int **)(param_1 + 100),*(undefined4 *)(puVar7 + -0x7fdc),
               *(undefined2 *)(**(int **)(puVar7 + -0x7fd8) + 4));
    (*(code *)**(undefined4 **)(**(int **)(param_1 + 100) + 0xc))
              (*(int **)(param_1 + 100),*(undefined4 *)(puVar7 + -0x7fd4),
               **(undefined4 **)(param_1 + 0x6c));
    *(undefined1 *)(iVar9 + 4) = 0;
    (*(code *)**(undefined4 **)(**(int **)(param_1 + 100) + 0x14))(*(int **)(param_1 + 100));
  }
LAB_00c2eb38:
  *(undefined4 *)(param_1 + 0x188) = 12000;
  *(undefined4 *)(param_1 + 0x70) = 2;
  return;
}


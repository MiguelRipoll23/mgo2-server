// hook SITE 0x00e9ea00

undefined4 _opd_FUN_00e9e238(int param_1,undefined *param_2,undefined4 *param_3)

{
  bool bVar1;
  int iVar2;
  int iVar3;
  undefined4 *puVar4;
  undefined4 *puVar5;
  undefined *puVar6;
  undefined4 uVar7;
  uint uVar8;
  byte *pbVar9;
  uint uVar12;
  longlong lVar10;
  ulonglong uVar11;
  int iVar13;
  int iVar14;
  uint uVar15;
  int iVar16;
  int iVar17;
  int *piVar18;
  undefined4 uVar19;
  undefined4 uVar20;
  undefined4 uVar21;
  undefined4 uVar22;
  undefined4 uVar23;
  undefined4 uVar24;
  undefined4 uVar25;
  undefined4 uVar26;
  undefined4 uVar27;
  undefined4 uVar28;
  undefined4 uVar29;
  undefined4 uVar30;
  undefined4 uVar31;
  undefined4 uVar32;
  undefined1 local_d0 [16];
  undefined1 local_c0 [16];
  undefined1 local_b0 [80];
  
  puVar6 = PTR_PTR_0121c31c;
  iVar2 = *(int *)(param_1 + 0x60);
  if (param_2 == &UNK_003d0006) {
    *param_3 = *(undefined4 *)(iVar2 + 0x10);
    return 0;
  }
  if (param_2 < &UNK_003d0007) {
    if (param_2 == &DAT_003d0003) {
      *(undefined4 **)(iVar2 + 0x9b0) = param_3;
      if (param_3 == (undefined4 *)0xffffffff) {
        return 0;
      }
      _opd_FUN_0060cfb8(*(undefined4 *)(iVar2 + 0x10),iVar2 + 0x930,*(undefined4 *)(iVar2 + 0x9bc),
                        *(undefined4 *)(iVar2 + 0x9b0),0,local_b0);
      return 0;
    }
    if (&DAT_003d0004 <= param_2) {
      if (param_2 == &DAT_003d0004) {
        _opd_FUN_0016abf0(iVar2 + 0x310);
        return 0;
      }
      if (param_2 != &DAT_003d0005) {
        return 0;
      }
      _opd_FUN_000c1e00(param_1);
      return 4;
    }
    if (param_2 != &UNK_003d0001) {
      if (param_2 != &UNK_003d0002) {
        return 0;
      }
      if (*(undefined4 **)(iVar2 + 0xa30) == param_3) {
        return 0;
      }
      _opd_FUN_00cd1270(iVar2 + 0x10,*(undefined4 *)(iVar2 + 0x9b4),0,param_3,0,local_d0,0,0x100);
      _opd_FUN_00d4e0f0(*(undefined4 *)(iVar2 + 0x14),0);
      *(undefined4 **)(iVar2 + 0xa30) = param_3;
      *(uint *)(iVar2 + 0xa34) = *(uint *)(iVar2 + 0xa34) & 0xfffffffe;
      return 0;
    }
    piVar18 = (int *)(iVar2 + 0x480);
    _opd_FUN_00e991e8(iVar2,param_3);
    iVar17 = 0;
    iVar13 = *(int *)(iVar2 + 0x10);
    iVar16 = 0x4d0;
    do {
      iVar3 = *piVar18;
      if (iVar3 != 0) {
        if (piVar18[0x122] == 0) {
          if (*(int *)(iVar3 + 0xac) != 0) {
            uVar8 = 0;
            pbVar9 = (byte *)(iVar2 + iVar16);
            do {
              iVar14 = uVar8 * 0x40;
              uVar8 = uVar8 + 1;
              uVar15 = iVar14 + *(int *)(iVar3 + 0xd4);
              uVar12 = (uint)*pbVar9 * 0x40 + *(int *)(iVar13 + 0xd4);
              puVar4 = (undefined4 *)(uVar12 + 0x30 & 0xfffffff0);
              uVar7 = puVar4[1];
              uVar19 = puVar4[2];
              uVar20 = puVar4[3];
              puVar5 = (undefined4 *)(uVar12 & 0xfffffff0);
              uVar21 = *puVar5;
              uVar22 = puVar5[1];
              uVar23 = puVar5[2];
              uVar24 = puVar5[3];
              puVar5 = (undefined4 *)(uVar12 + 0x10 & 0xfffffff0);
              uVar29 = *puVar5;
              uVar30 = puVar5[1];
              uVar31 = puVar5[2];
              uVar32 = puVar5[3];
              puVar5 = (undefined4 *)(uVar12 + 0x20 & 0xfffffff0);
              uVar25 = *puVar5;
              uVar26 = puVar5[1];
              uVar27 = puVar5[2];
              uVar28 = puVar5[3];
              puVar5 = (undefined4 *)(uVar15 + 0x30 & 0xfffffff0);
              *puVar5 = *puVar4;
              puVar5[1] = uVar7;
              puVar5[2] = uVar19;
              puVar5[3] = uVar20;
              puVar4 = (undefined4 *)(uVar15 & 0xfffffff0);
              *puVar4 = uVar21;
              puVar4[1] = uVar22;
              puVar4[2] = uVar23;
              puVar4[3] = uVar24;
              puVar4 = (undefined4 *)(uVar15 + 0x10 & 0xfffffff0);
              *puVar4 = uVar29;
              puVar4[1] = uVar30;
              puVar4[2] = uVar31;
              puVar4[3] = uVar32;
              puVar4 = (undefined4 *)(uVar15 + 0x20 & 0xfffffff0);
              *puVar4 = uVar25;
              puVar4[1] = uVar26;
              puVar4[2] = uVar27;
              puVar4[3] = uVar28;
              pbVar9 = pbVar9 + 1;
            } while (uVar8 < *(uint *)(iVar3 + 0xac));
          }
          uVar8 = *(uint *)(iVar3 + 0xd4);
          puVar4 = (undefined4 *)(uVar8 + 0x30 & 0xfffffff0);
          uVar25 = *puVar4;
          uVar26 = puVar4[1];
          uVar27 = puVar4[2];
          uVar28 = puVar4[3];
          puVar4 = (undefined4 *)(uVar8 & 0xfffffff0);
          uVar7 = puVar4[1];
          uVar19 = puVar4[2];
          uVar20 = puVar4[3];
          puVar5 = (undefined4 *)(uVar8 + 0x10 & 0xfffffff0);
          uVar21 = *puVar5;
          uVar22 = puVar5[1];
          uVar23 = puVar5[2];
          uVar24 = puVar5[3];
          puVar5 = (undefined4 *)(uVar8 + 0x20 & 0xfffffff0);
          uVar29 = *puVar5;
          uVar30 = puVar5[1];
          uVar31 = puVar5[2];
          uVar32 = puVar5[3];
          puVar5 = (undefined4 *)(iVar3 + 0x10U & 0xfffffff0);
          *puVar5 = *puVar4;
          puVar5[1] = uVar7;
          puVar5[2] = uVar19;
          puVar5[3] = uVar20;
          puVar4 = (undefined4 *)(iVar3 + 0x20U & 0xfffffff0);
          *puVar4 = uVar21;
          puVar4[1] = uVar22;
          puVar4[2] = uVar23;
          puVar4[3] = uVar24;
          puVar4 = (undefined4 *)(iVar3 + 0x30U & 0xfffffff0);
          *puVar4 = uVar29;
          puVar4[1] = uVar30;
          puVar4[2] = uVar31;
          puVar4[3] = uVar32;
          puVar4 = (undefined4 *)(iVar3 + 0x40U & 0xfffffff0);
          *puVar4 = uVar25;
          puVar4[1] = uVar26;
          puVar4[2] = uVar27;
          puVar4[3] = uVar28;
        }
        *(int *)(iVar3 + 0x54) = iVar2 + 0x80;
      }
      bVar1 = iVar17 != 9;
      iVar16 = iVar16 + 100;
      piVar18 = piVar18 + 1;
      iVar17 = iVar17 + 1;
    } while (bVar1);
    iVar13 = 0;
    if ((*(uint *)(iVar2 + 0xa14) & 1) != 0) {
LAB_00e9eb14:
      lVar10 = (ulonglong)(9 - iVar13) + 1;
      if (10 < iVar13 + 1U) {
        lVar10 = 1;
      }
      do {
        iVar16 = *(int *)(iVar2 + iVar13 * 4 + 0x480);
        if ((iVar16 != 0) &&
           (*(uint *)(iVar16 + 0x5c) = *(uint *)(iVar16 + 0x5c) & 0xfffffff8,
           *(int *)(iVar2 + 0xa48) - 1U < 2)) {
          if (iVar13 == 5) {
            iVar16 = *(int *)(iVar2 + 0x444);
            if ((iVar16 != 0x2b) && ((iVar16 != 0x83 && (iVar16 != 0x8e)))) goto code_r0x00e9ebc0;
          }
          else if ((iVar13 < 5) || (7 < iVar13)) goto LAB_00e9eb5c;
          iVar16 = *(int *)(iVar2 + iVar13 * 4 + 0x480);
          *(uint *)(iVar16 + 0x5c) = *(uint *)(iVar16 + 0x5c) | 7;
        }
LAB_00e9eb5c:
        iVar13 = iVar13 + 1;
        lVar10 = lVar10 + -1;
        if (lVar10 == 0) {
          return 0;
        }
      } while( true );
    }
  }
  else {
    if (param_2 != &UNK_003d0009) {
      if (param_2 < &DAT_003d000a) {
        if (param_2 == &UNK_003d0007) {
          if (*(undefined4 **)(iVar2 + 0xa30) == param_3) {
            return 0;
          }
          _opd_FUN_00cd1270(iVar2 + 0x10,*(undefined4 *)(iVar2 + 0x9b4),0,param_3,0,local_c0,0,0x100
                           );
          _opd_FUN_00d4e098(*(undefined4 *)(iVar2 + 0x14),0);
          *(undefined4 **)(iVar2 + 0xa30) = param_3;
          *(uint *)(iVar2 + 0xa34) = *(uint *)(iVar2 + 0xa34) | 1;
          return 0;
        }
        if (param_2 != &UNK_003d0008) {
          return 0;
        }
        if ((*(uint *)(iVar2 + 0xa34) & 1) != 0) {
          *param_3 = 0xffffffff;
          return 0;
        }
        if ((*(uint *)(*(int *)(*(int *)(iVar2 + 0x14) + 4) + 4) & 1) != 0) {
          *param_3 = 1;
          return 0;
        }
        *param_3 = 0;
        return 0;
      }
      if (param_2 == (undefined *)0xfffe0001) {
        *(uint *)(iVar2 + 0xa14) = *(uint *)(iVar2 + 0xa14) | 1;
        return 0;
      }
      if (param_2 == (undefined *)0xfffe0002) {
        *(uint *)(iVar2 + 0xa14) = *(uint *)(iVar2 + 0xa14) & 0xfffffffe;
        return 0;
      }
      if (param_2 != &DAT_003d0016) {
        return 0;
      }
      iVar13 = *(int *)(iVar2 + 0x494);
      if (iVar13 == 0) {
        return 0;
      }
      uVar11 = (ulonglong)*(uint *)(iVar2 + 0x444) - 0x84;
      if (8 < (uVar11 & 0xffffffff)) {
        return 0;
      }
      if ((1L << (uVar11 & 0x7f) & 0x183U) == 0) {
        return 0;
      }
      uVar11 = *(ulonglong *)(iVar2 + 0xb48);
      if ((longlong)uVar11 < 0) {
        uVar7 = *(undefined4 *)(PTR_PTR_0121c31c + -0x7fec);
        *(ulonglong *)(iVar2 + 0xb48) = uVar11 & 0x7fffffffffffffff;
        uVar7 = _opd_FUN_000cdc90(uVar7);
        _opd_FUN_00113560(iVar13,uVar7);
        uVar7 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fe8));
        _opd_FUN_00113118(iVar13,uVar7);
      }
      else {
        uVar7 = *(undefined4 *)(PTR_PTR_0121c31c + -0x7fec);
        *(ulonglong *)(iVar2 + 0xb48) = uVar11 | 0x8000000000000000;
        uVar7 = _opd_FUN_000cdc90(uVar7);
        _opd_FUN_00113118(iVar13,uVar7);
        uVar7 = _opd_FUN_000cdc90(*(undefined4 *)(puVar6 + -0x7fe8));
        _opd_FUN_00113560(iVar13,uVar7);
      }
      _opd_FUN_000482b0(0x22a);
      return 0;
    }
    iVar17 = 0;
    *(undefined4 *)(iVar2 + 0x4a8) = *param_3;
    *(undefined4 *)(iVar2 + 0x4ac) = param_3[1];
    *(undefined4 *)(iVar2 + 0x4b0) = param_3[2];
    iVar16 = 0x4d0;
    *(undefined4 *)(iVar2 + 0x4b4) = param_3[3];
    *(undefined4 *)(iVar2 + 0x4b8) = param_3[4];
    *(undefined4 *)(iVar2 + 0x4bc) = param_3[5];
    *(undefined4 *)(iVar2 + 0x4c0) = param_3[6];
    *(undefined4 *)(iVar2 + 0x4c4) = param_3[7];
    *(undefined4 *)(iVar2 + 0x4c8) = param_3[8];
    *(undefined4 *)(iVar2 + 0x4cc) = param_3[9];
    _opd_FUN_00e991e8(iVar2,iVar2 + 0x430);
    piVar18 = (int *)(iVar2 + 0x480);
    iVar13 = *(int *)(iVar2 + 0x10);
    do {
      iVar3 = *piVar18;
      if (iVar3 != 0) {
        if (piVar18[0x122] == 0) {
          if (*(int *)(iVar3 + 0xac) != 0) {
            uVar8 = 0;
            pbVar9 = (byte *)(iVar2 + iVar16);
            do {
              iVar14 = uVar8 * 0x40;
              uVar8 = uVar8 + 1;
              uVar15 = iVar14 + *(int *)(iVar3 + 0xd4);
              uVar12 = (uint)*pbVar9 * 0x40 + *(int *)(iVar13 + 0xd4);
              puVar4 = (undefined4 *)(uVar12 + 0x30 & 0xfffffff0);
              uVar7 = puVar4[1];
              uVar19 = puVar4[2];
              uVar20 = puVar4[3];
              puVar5 = (undefined4 *)(uVar12 & 0xfffffff0);
              uVar21 = *puVar5;
              uVar22 = puVar5[1];
              uVar23 = puVar5[2];
              uVar24 = puVar5[3];
              puVar5 = (undefined4 *)(uVar12 + 0x10 & 0xfffffff0);
              uVar29 = *puVar5;
              uVar30 = puVar5[1];
              uVar31 = puVar5[2];
              uVar32 = puVar5[3];
              puVar5 = (undefined4 *)(uVar12 + 0x20 & 0xfffffff0);
              uVar25 = *puVar5;
              uVar26 = puVar5[1];
              uVar27 = puVar5[2];
              uVar28 = puVar5[3];
              puVar5 = (undefined4 *)(uVar15 + 0x30 & 0xfffffff0);
              *puVar5 = *puVar4;
              puVar5[1] = uVar7;
              puVar5[2] = uVar19;
              puVar5[3] = uVar20;
              puVar4 = (undefined4 *)(uVar15 & 0xfffffff0);
              *puVar4 = uVar21;
              puVar4[1] = uVar22;
              puVar4[2] = uVar23;
              puVar4[3] = uVar24;
              puVar4 = (undefined4 *)(uVar15 + 0x10 & 0xfffffff0);
              *puVar4 = uVar29;
              puVar4[1] = uVar30;
              puVar4[2] = uVar31;
              puVar4[3] = uVar32;
              puVar4 = (undefined4 *)(uVar15 + 0x20 & 0xfffffff0);
              *puVar4 = uVar25;
              puVar4[1] = uVar26;
              puVar4[2] = uVar27;
              puVar4[3] = uVar28;
              pbVar9 = pbVar9 + 1;
            } while (uVar8 < *(uint *)(iVar3 + 0xac));
          }
          uVar8 = *(uint *)(iVar3 + 0xd4);
          puVar4 = (undefined4 *)(uVar8 + 0x30 & 0xfffffff0);
          uVar25 = *puVar4;
          uVar26 = puVar4[1];
          uVar27 = puVar4[2];
          uVar28 = puVar4[3];
          puVar4 = (undefined4 *)(uVar8 & 0xfffffff0);
          uVar7 = puVar4[1];
          uVar19 = puVar4[2];
          uVar20 = puVar4[3];
          puVar5 = (undefined4 *)(uVar8 + 0x10 & 0xfffffff0);
          uVar21 = *puVar5;
          uVar22 = puVar5[1];
          uVar23 = puVar5[2];
          uVar24 = puVar5[3];
          puVar5 = (undefined4 *)(uVar8 + 0x20 & 0xfffffff0);
          uVar29 = *puVar5;
          uVar30 = puVar5[1];
          uVar31 = puVar5[2];
          uVar32 = puVar5[3];
          puVar5 = (undefined4 *)(iVar3 + 0x10U & 0xfffffff0);
          *puVar5 = *puVar4;
          puVar5[1] = uVar7;
          puVar5[2] = uVar19;
          puVar5[3] = uVar20;
          puVar4 = (undefined4 *)(iVar3 + 0x20U & 0xfffffff0);
          *puVar4 = uVar21;
          puVar4[1] = uVar22;
          puVar4[2] = uVar23;
          puVar4[3] = uVar24;
          puVar4 = (undefined4 *)(iVar3 + 0x30U & 0xfffffff0);
          *puVar4 = uVar29;
          puVar4[1] = uVar30;
          puVar4[2] = uVar31;
          puVar4[3] = uVar32;
          puVar4 = (undefined4 *)(iVar3 + 0x40U & 0xfffffff0);
          *puVar4 = uVar25;
          puVar4[1] = uVar26;
          puVar4[2] = uVar27;
          puVar4[3] = uVar28;
        }
        *(int *)(iVar3 + 0x54) = iVar2 + 0x80;
      }
      bVar1 = iVar17 != 9;
      iVar16 = iVar16 + 100;
      piVar18 = piVar18 + 1;
      iVar17 = iVar17 + 1;
    } while (bVar1);
    iVar13 = 0;
    if ((*(uint *)(iVar2 + 0xa14) & 1) != 0) {
LAB_00e9e674:
      lVar10 = (ulonglong)(9 - iVar13) + 1;
      if (10 < iVar13 + 1U) {
        lVar10 = 1;
      }
      do {
        iVar16 = *(int *)(iVar2 + iVar13 * 4 + 0x480);
        if ((iVar16 != 0) &&
           (*(uint *)(iVar16 + 0x5c) = *(uint *)(iVar16 + 0x5c) & 0xfffffff8,
           *(int *)(iVar2 + 0xa48) - 1U < 2)) {
          if (iVar13 == 5) {
            iVar16 = *(int *)(iVar2 + 0x444);
            if (((iVar16 != 0x2b) && (iVar16 != 0x83)) && (iVar16 != 0x8e)) goto code_r0x00e9e848;
          }
          else if ((iVar13 < 5) || (7 < iVar13)) goto LAB_00e9e6e0;
          iVar16 = *(int *)(iVar2 + iVar13 * 4 + 0x480);
          *(uint *)(iVar16 + 0x5c) = *(uint *)(iVar16 + 0x5c) | 7;
        }
LAB_00e9e6e0:
        iVar13 = iVar13 + 1;
        lVar10 = lVar10 + -1;
        if (lVar10 == 0) {
          return 0;
        }
      } while( true );
    }
  }
  iVar13 = *(int *)(iVar2 + 0x480);
  if (iVar13 != 0) {
    *(uint *)(iVar13 + 0x5c) = *(uint *)(iVar13 + 0x5c) | 7;
  }
  iVar13 = *(int *)(iVar2 + 0x484);
  if (iVar13 != 0) {
    *(uint *)(iVar13 + 0x5c) = *(uint *)(iVar13 + 0x5c) | 7;
  }
  iVar13 = *(int *)(iVar2 + 0x488);
  if (iVar13 != 0) {
    *(uint *)(iVar13 + 0x5c) = *(uint *)(iVar13 + 0x5c) | 7;
  }
  iVar13 = *(int *)(iVar2 + 0x48c);
  if (iVar13 != 0) {
    *(uint *)(iVar13 + 0x5c) = *(uint *)(iVar13 + 0x5c) | 7;
  }
  iVar13 = *(int *)(iVar2 + 0x490);
  if (iVar13 != 0) {
    *(uint *)(iVar13 + 0x5c) = *(uint *)(iVar13 + 0x5c) | 7;
  }
  iVar13 = *(int *)(iVar2 + 0x494);
  if (iVar13 != 0) {
    *(uint *)(iVar13 + 0x5c) = *(uint *)(iVar13 + 0x5c) | 7;
  }
  iVar13 = *(int *)(iVar2 + 0x498);
  if (iVar13 != 0) {
    *(uint *)(iVar13 + 0x5c) = *(uint *)(iVar13 + 0x5c) | 7;
  }
  iVar13 = *(int *)(iVar2 + 0x49c);
  if (iVar13 != 0) {
    *(uint *)(iVar13 + 0x5c) = *(uint *)(iVar13 + 0x5c) | 7;
  }
  iVar13 = *(int *)(iVar2 + 0x4a0);
  if (iVar13 != 0) {
    *(uint *)(iVar13 + 0x5c) = *(uint *)(iVar13 + 0x5c) | 7;
  }
  iVar2 = *(int *)(iVar2 + 0x4a4);
  if (iVar2 == 0) {
    return 0;
  }
  *(uint *)(iVar2 + 0x5c) = *(uint *)(iVar2 + 0x5c) | 7;
  return 0;
code_r0x00e9ebc0:
  iVar13 = 6;
  goto LAB_00e9eb14;
code_r0x00e9e848:
  iVar13 = 6;
  goto LAB_00e9e674;
}


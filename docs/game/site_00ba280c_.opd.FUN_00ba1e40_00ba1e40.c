// hook SITE 0x00ba280c

longlong _opd_FUN_00ba1e40(void)

{
  bool bVar1;
  int *piVar2;
  byte *pbVar3;
  uint uVar4;
  undefined4 *puVar5;
  undefined *puVar6;
  int iVar8;
  longlong lVar7;
  int iVar9;
  int iVar10;
  undefined4 uVar11;
  byte bVar13;
  undefined4 uVar12;
  char cVar14;
  int *piVar15;
  undefined *puVar16;
  undefined8 uVar17;
  undefined4 local_80 [4];
  undefined4 local_70;
  undefined4 local_6c;
  undefined4 local_68;
  undefined4 local_64;
  
  puVar6 = PTR_DAT_0121bd78;
  uVar17 = *(undefined8 *)(PTR_DAT_0121bd78 + -0x7f88);
  iVar8 = _opd_FUN_000be778(0xb0,uVar17);
  if (iVar8 != 0) {
    lVar7 = _opd_FUN_000bead8(iVar8,uVar17,*(undefined4 *)(puVar6 + -0x7f80),
                              *(undefined4 *)(puVar6 + -0x7f7c));
    if ((lVar7 != 0) && (piVar2 = *(int **)(puVar6 + -0x8000), *piVar2 == 0)) {
      iVar9 = _opd_FUN_00280418();
      *(int *)(iVar8 + 0x60) = iVar9;
      if ((iVar9 != 0) &&
         ((*(int *)(iVar8 + 0x88) = iVar9 + 0x19f44, iVar9 + 0x19f44 != 0 &&
          (*(int *)(iVar8 + 0x8c) = iVar9 + 0x1a234, iVar9 + 0x1a234 != 0)))) {
        if (*(char *)(iVar9 + 0x1a244) != '\0') {
          *(int *)(iVar8 + 0x94) = *(int *)(iVar8 + 0x94) + 1;
        }
        iVar9 = *(int *)(iVar8 + 0x8c);
        if (*(char *)(iVar9 + 0x11) != '\0') {
          *(int *)(iVar8 + 0x94) = *(int *)(iVar8 + 0x94) + 1;
        }
        if (*(char *)(iVar9 + 0x12) != '\0') {
          *(int *)(iVar8 + 0x94) = *(int *)(iVar8 + 0x94) + 1;
        }
        if (*(char *)(iVar9 + 0x13) != '\0') {
          *(int *)(iVar8 + 0x94) = *(int *)(iVar8 + 0x94) + 1;
        }
        if (*(char *)(iVar9 + 0x14) != '\0') {
          *(int *)(iVar8 + 0x94) = *(int *)(iVar8 + 0x94) + 1;
        }
        if (*(char *)(iVar9 + 0x15) != '\0') {
          *(int *)(iVar8 + 0x94) = *(int *)(iVar8 + 0x94) + 1;
        }
        if (*(char *)(iVar9 + 0x16) != '\0') {
          *(int *)(iVar8 + 0x94) = *(int *)(iVar8 + 0x94) + 1;
        }
        if (*(char *)(iVar9 + 0x17) != '\0') {
          *(int *)(iVar8 + 0x94) = *(int *)(iVar8 + 0x94) + 1;
        }
        if (*(char *)(iVar9 + 0x18) != '\0') {
          *(int *)(iVar8 + 0x94) = *(int *)(iVar8 + 0x94) + 1;
        }
        pbVar3 = *(byte **)(iVar8 + 0x8c);
        if (pbVar3[0x19] != 0) {
          *(int *)(iVar8 + 0x94) = *(int *)(iVar8 + 0x94) + 1;
        }
        if (pbVar3[0x1a] != 0) {
          *(int *)(iVar8 + 0x94) = *(int *)(iVar8 + 0x94) + 1;
        }
        if (pbVar3[0x1b] != 0) {
          *(int *)(iVar8 + 0x94) = *(int *)(iVar8 + 0x94) + 1;
        }
        if (pbVar3[0x1c] != 0) {
          *(int *)(iVar8 + 0x94) = *(int *)(iVar8 + 0x94) + 1;
        }
        if (pbVar3[0x1d] != 0) {
          *(int *)(iVar8 + 0x94) = *(int *)(iVar8 + 0x94) + 1;
        }
        if (pbVar3[0x1e] != 0) {
          *(int *)(iVar8 + 0x94) = *(int *)(iVar8 + 0x94) + 1;
        }
        if (pbVar3[0x1f] != 0) {
          *(int *)(iVar8 + 0x94) = *(int *)(iVar8 + 0x94) + 1;
        }
        if (pbVar3[0x10] != 0) {
          *(uint *)(iVar8 + 0x98) = *(uint *)(iVar8 + 0x98) | 1 << (*pbVar3 & 0x3f);
        }
        if (pbVar3[0x11] != 0) {
          *(uint *)(iVar8 + 0x98) = *(uint *)(iVar8 + 0x98) | 1 << (pbVar3[1] & 0x3f);
        }
        if (pbVar3[0x12] != 0) {
          *(uint *)(iVar8 + 0x98) = *(uint *)(iVar8 + 0x98) | 1 << (pbVar3[2] & 0x3f);
        }
        iVar9 = *(int *)(iVar8 + 0x8c);
        if (*(char *)(iVar9 + 0x13) != '\0') {
          *(uint *)(iVar8 + 0x98) = *(uint *)(iVar8 + 0x98) | 1 << (*(byte *)(iVar9 + 3) & 0x3f);
        }
        if (*(char *)(iVar9 + 0x14) != '\0') {
          *(uint *)(iVar8 + 0x98) = *(uint *)(iVar8 + 0x98) | 1 << (*(byte *)(iVar9 + 4) & 0x3f);
        }
        if (*(char *)(iVar9 + 0x15) != '\0') {
          *(uint *)(iVar8 + 0x98) = *(uint *)(iVar8 + 0x98) | 1 << (*(byte *)(iVar9 + 5) & 0x3f);
        }
        if (*(char *)(iVar9 + 0x16) != '\0') {
          *(uint *)(iVar8 + 0x98) = *(uint *)(iVar8 + 0x98) | 1 << (*(byte *)(iVar9 + 6) & 0x3f);
        }
        if (*(char *)(iVar9 + 0x17) != '\0') {
          *(uint *)(iVar8 + 0x98) = *(uint *)(iVar8 + 0x98) | 1 << (*(byte *)(iVar9 + 7) & 0x3f);
        }
        if (*(char *)(iVar9 + 0x18) != '\0') {
          *(uint *)(iVar8 + 0x98) = *(uint *)(iVar8 + 0x98) | 1 << (*(byte *)(iVar9 + 8) & 0x3f);
        }
        if (*(char *)(iVar9 + 0x19) != '\0') {
          *(uint *)(iVar8 + 0x98) = *(uint *)(iVar8 + 0x98) | 1 << (*(byte *)(iVar9 + 9) & 0x3f);
        }
        if (*(char *)(iVar9 + 0x1a) != '\0') {
          *(uint *)(iVar8 + 0x98) = *(uint *)(iVar8 + 0x98) | 1 << (*(byte *)(iVar9 + 10) & 0x3f);
        }
        if (*(char *)(iVar9 + 0x1b) != '\0') {
          *(uint *)(iVar8 + 0x98) = *(uint *)(iVar8 + 0x98) | 1 << (*(byte *)(iVar9 + 0xb) & 0x3f);
        }
        if (*(char *)(iVar9 + 0x1c) != '\0') {
          *(uint *)(iVar8 + 0x98) = *(uint *)(iVar8 + 0x98) | 1 << (*(byte *)(iVar9 + 0xc) & 0x3f);
        }
        iVar9 = *(int *)(iVar8 + 0x8c);
        if (*(char *)(iVar9 + 0x1d) == '\0') {
          uVar4 = *(uint *)(iVar8 + 0x98);
        }
        else {
          uVar4 = *(uint *)(iVar8 + 0x98) | 1 << (*(byte *)(iVar9 + 0xd) & 0x3f);
          *(uint *)(iVar8 + 0x98) = uVar4;
        }
        if (*(char *)(iVar9 + 0x1e) != '\0') {
          uVar4 = uVar4 | 1 << (*(byte *)(iVar9 + 0xe) & 0x3f);
          *(uint *)(iVar8 + 0x98) = uVar4;
        }
        if (*(char *)(iVar9 + 0x1f) != '\0') {
          uVar4 = uVar4 | 1 << (*(byte *)(iVar9 + 0xf) & 0x3f);
          *(uint *)(iVar8 + 0x98) = uVar4;
        }
        if ((uVar4 & 1) != 0) {
          *(int *)(iVar8 + 0x9c) = *(int *)(iVar8 + 0x9c) + 1;
        }
        if ((uVar4 >> 1 & 1) != 0) {
          *(int *)(iVar8 + 0x9c) = *(int *)(iVar8 + 0x9c) + 1;
        }
        if ((uVar4 >> 2 & 1) != 0) {
          *(int *)(iVar8 + 0x9c) = *(int *)(iVar8 + 0x9c) + 1;
        }
        if ((uVar4 >> 3 & 1) != 0) {
          *(int *)(iVar8 + 0x9c) = *(int *)(iVar8 + 0x9c) + 1;
        }
        if ((uVar4 >> 4 & 1) != 0) {
          *(int *)(iVar8 + 0x9c) = *(int *)(iVar8 + 0x9c) + 1;
        }
        if ((uVar4 >> 5 & 1) != 0) {
          *(int *)(iVar8 + 0x9c) = *(int *)(iVar8 + 0x9c) + 1;
        }
        if ((uVar4 >> 6 & 1) != 0) {
          *(int *)(iVar8 + 0x9c) = *(int *)(iVar8 + 0x9c) + 1;
        }
        uVar4 = *(uint *)(iVar8 + 0x98);
        if ((uVar4 >> 7 & 1) != 0) {
          *(int *)(iVar8 + 0x9c) = *(int *)(iVar8 + 0x9c) + 1;
        }
        if ((uVar4 >> 8 & 1) != 0) {
          *(int *)(iVar8 + 0x9c) = *(int *)(iVar8 + 0x9c) + 1;
        }
        if ((uVar4 >> 9 & 1) != 0) {
          *(int *)(iVar8 + 0x9c) = *(int *)(iVar8 + 0x9c) + 1;
        }
        if ((uVar4 >> 10 & 1) != 0) {
          *(int *)(iVar8 + 0x9c) = *(int *)(iVar8 + 0x9c) + 1;
        }
        if ((uVar4 >> 0xb & 1) != 0) {
          *(int *)(iVar8 + 0x9c) = *(int *)(iVar8 + 0x9c) + 1;
        }
        if ((uVar4 >> 0xc & 1) != 0) {
          *(int *)(iVar8 + 0x9c) = *(int *)(iVar8 + 0x9c) + 1;
        }
        if ((uVar4 >> 0xd & 1) != 0) {
          *(int *)(iVar8 + 0x9c) = *(int *)(iVar8 + 0x9c) + 1;
        }
        if ((uVar4 >> 0xe & 1) != 0) {
          *(int *)(iVar8 + 0x9c) = *(int *)(iVar8 + 0x9c) + 1;
        }
        if ((uVar4 >> 0xf & 1) != 0) {
          *(int *)(iVar8 + 0x9c) = *(int *)(iVar8 + 0x9c) + 1;
        }
        if ((uVar4 >> 0x10 & 1) != 0) {
          *(int *)(iVar8 + 0x9c) = *(int *)(iVar8 + 0x9c) + 1;
        }
        puVar5 = *(undefined4 **)(puVar6 + -0x7ffc);
        local_64 = puVar5[7];
        local_80[0] = *puVar5;
        local_80[1] = puVar5[1];
        local_80[2] = puVar5[2];
        local_80[3] = puVar5[3];
        local_70 = puVar5[4];
        local_6c = puVar5[5];
        local_68 = puVar5[6];
        iVar9 = _opd_FUN_00242398(0x748bb1,2,2,0xc3,1,0,0);
        *(int *)(iVar8 + 100) = iVar9;
        if (iVar9 != 0) {
          iVar9 = 0;
          piVar15 = (int *)(iVar8 + 0x68);
          do {
            iVar10 = _opd_FUN_00242398(0x8e5482,2,2,0xc3,1,0,0);
            *piVar15 = iVar10;
            if (iVar10 == 0) goto LAB_00ba1ed8;
            uVar11 = _opd_FUN_00242bf0(*(undefined4 *)(iVar8 + 100),
                                       *(undefined4 *)((int)local_80 + iVar9));
            _opd_FUN_00243718(iVar10,uVar11);
            _opd_FUN_002438e0(*piVar15);
            _opd_FUN_0024d078((double)*(float *)(puVar6 + -0x7ff8),*piVar15);
            bVar1 = iVar9 != 0x1c;
            iVar9 = iVar9 + 4;
            piVar15 = piVar15 + 1;
          } while (bVar1);
          uVar11 = _opd_FUN_00242bf0(*(undefined4 *)(iVar8 + 100),0x51d14d);
          _opd_FUN_002495a8(*(undefined4 *)(iVar8 + 100),&DAT_00f92c4b,uVar11);
          _opd_FUN_002495a8(*(undefined4 *)(iVar8 + 100),&DAT_0084c8c6,uVar11);
          _opd_FUN_002495a8(*(undefined4 *)(iVar8 + 100),0xa8270,uVar11);
          _opd_FUN_002495a8(*(undefined4 *)(iVar8 + 100),0x1d867a,uVar11);
          _opd_FUN_002495a8(*(undefined4 *)(iVar8 + 100),0x2de143,uVar11);
          _opd_FUN_002495a8(*(undefined4 *)(iVar8 + 100),0xe26b90,uVar11);
          _opd_FUN_00248d90(*(undefined4 *)(iVar8 + 100));
          _opd_FUN_0024d078((double)*(float *)(puVar6 + -0x7ff8),*(undefined4 *)(iVar8 + 100));
          _opd_FUN_00b30830(*(undefined4 *)(iVar8 + 100),0x222811);
          _opd_FUN_00b04ad8(*(undefined4 *)(iVar8 + 100),0xe12a71);
          iVar9 = _opd_FUN_00b31810();
          if (iVar9 == 0) {
            _opd_FUN_00b30830(*(undefined4 *)(iVar8 + 100),0xc6e090);
          }
          else {
            _opd_FUN_00b30880(*(undefined4 *)(iVar8 + 100),0xc6e090);
          }
          bVar13 = _opd_FUN_00b17498();
          iVar9 = _opd_FUN_00b18bd8();
          puVar16 = (undefined *)0xe26b90;
          if (iVar9 == 0) {
            iVar9 = _opd_FUN_00b1e138();
            puVar16 = (undefined *)0x2de143;
            if ((iVar9 == 0) && (puVar16 = (undefined *)0x1d867a, 1 < (byte)(bVar13 - 5))) {
              puVar16 = &DAT_0084c8c6;
              if (bVar13 != 3) {
                puVar16 = (undefined *)(((int)-(bVar13 ^ 4) >> 0x1f & 0xeea9dbU) + 0xa8270);
              }
            }
          }
          uVar11 = *(undefined4 *)(iVar8 + 100);
          iVar9 = 0;
          uVar12 = _opd_FUN_00242bf0(uVar11,0x51d14d);
          _opd_FUN_00b04938(uVar11,puVar16,uVar12);
          _opd_FUN_0023ca38(*(undefined4 *)(iVar8 + 100));
          uVar11 = *(undefined4 *)(iVar8 + 100);
          uVar12 = _opd_FUN_00b30b78(0x16);
          _opd_FUN_00b310a0(uVar11,0x2107e8,uVar12,0);
          _opd_FUN_0024d078((double)*(float *)(puVar6 + -0x7ff8),*(undefined4 *)(iVar8 + 100));
          *(undefined1 *)(iVar8 + 0x92) = 0;
          *(undefined1 *)(iVar8 + 0x91) = 0;
          *piVar2 = iVar8;
          *(undefined4 *)(iVar8 + 0xa8) = 0;
LAB_00ba27b8:
          do {
            if (*piVar2 == 0) {
LAB_00ba27dc:
              *(int *)(iVar8 + 0xa8) = *(int *)(iVar8 + 0xa8) + 1;
            }
            else {
              if (iVar9 == 6) {
                uVar17 = 4;
              }
              else {
                if (iVar9 != 0xf) {
                  if (iVar9 != 5) goto LAB_00ba27dc;
                  iVar9 = 6;
                  goto LAB_00ba27b8;
                }
                uVar17 = 5;
              }
              cVar14 = _opd_FUN_00f04238(*(undefined4 *)(*piVar2 + 0x60),uVar17);
              if (cVar14 != '\0') goto LAB_00ba27dc;
            }
            iVar9 = iVar9 + 1;
            if (0x14 < iVar9) {
              _opd_FUN_00b9fb40(iVar8);
              _opd_FUN_00b9e7d8(iVar8);
              _opd_FUN_00ba08d8(iVar8,4);
              _opd_FUN_00b30880(*(undefined4 *)(iVar8 + 100),0x1b4c98);
              return lVar7;
            }
          } while( true );
        }
      }
    }
LAB_00ba1ed8:
    _opd_FUN_000c1e00(iVar8);
  }
  return 0;
}


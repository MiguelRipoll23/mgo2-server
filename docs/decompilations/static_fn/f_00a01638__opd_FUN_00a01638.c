/* containing function of static patch target(s); entry .opd.FUN_00a01638 @ 00a01638 */

longlong _opd_FUN_00a01638(void)

{
  bool bVar1;
  undefined *puVar2;
  int iVar4;
  longlong lVar3;
  undefined4 uVar5;
  int iVar6;
  int iVar7;
  uint *puVar8;
  undefined4 uVar9;
  undefined4 uVar10;
  undefined4 uVar11;
  undefined4 uVar12;
  undefined4 uVar13;
  undefined4 uVar14;
  undefined4 uVar15;
  char cVar17;
  undefined4 uVar16;
  int *piVar18;
  undefined8 uVar19;
  
  puVar2 = PTR_PTR_0121bb40;
  uVar19 = *(undefined8 *)(PTR_PTR_0121bb40 + -0x8000);
  iVar4 = _opd_FUN_000be778(0xc1ac0,uVar19);
  if (iVar4 != 0) {
    lVar3 = _opd_FUN_000bead8(iVar4,uVar19,*(undefined4 *)(puVar2 + -0x7ff8),
                              *(undefined4 *)(puVar2 + -0x7ff4));
    if (lVar3 != 0) {
      **(int **)(puVar2 + -0x7ff0) = iVar4;
      uVar5 = _opd_FUN_00280418();
      *(undefined4 *)(iVar4 + 0x60) = uVar5;
      uVar5 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fec));
      iVar6 = _opd_FUN_00242358(uVar5,1,0,0x5a,1);
      *(int *)(iVar4 + 0xc1a78) = iVar6;
      if (iVar6 != 0) {
        iVar6 = 0;
        uVar5 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fe8));
        piVar18 = (int *)(iVar4 + 0xc1a7c);
        do {
          iVar7 = _opd_FUN_00242400(uVar5,1,0,0x6e);
          bVar1 = iVar6 != 0x28;
          iVar6 = iVar6 + 4;
          *piVar18 = iVar7;
          if (iVar7 == 0) goto LAB_00a02510;
          piVar18 = piVar18 + 1;
        } while (bVar1);
        iVar6 = _opd_FUN_00242400(uVar5,1,0,0x6e);
        *(int *)(iVar4 + 0xc1aac) = iVar6;
        if (iVar6 != 0) {
          puVar8 = (uint *)_opd_FUN_00ead010(iVar6,0x2a4634);
          if ((puVar8 != (uint *)0x0) && ((*puVar8 >> 0x16 & 1) != 0)) {
            *puVar8 = *puVar8 & 0xffbfffff | 0x40000000;
          }
          uVar5 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fe4));
          iVar6 = _opd_FUN_00242400(uVar5,1,0,0x6e);
          *(int *)(iVar4 + 0xc1ab0) = iVar6;
          if (iVar6 != 0) {
            puVar8 = (uint *)_opd_FUN_00ead010(iVar6,0x2a4634);
            if ((puVar8 != (uint *)0x0) && ((*puVar8 >> 0x16 & 1) != 0)) {
              *puVar8 = *puVar8 & 0xffbfffff | 0x40000000;
            }
            uVar5 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fe0));
            iVar6 = _opd_FUN_00242400(uVar5,1,0,100);
            *(int *)(iVar4 + 0xc1aa8) = iVar6;
            if (iVar6 != 0) {
              _opd_FUN_00a0ed68(*(undefined4 *)(iVar4 + 0xc1a78),0x44a,1099);
              uVar5 = *(undefined4 *)(iVar4 + 0xc1a78);
              uVar16 = _opd_FUN_009745d0(*(undefined4 *)(puVar2 + -0x7f40));
              _opd_FUN_00a0ca3c(uVar5,0x698386,uVar16);
              puVar8 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar4 + 0xc1a78),0x2a4634);
              if ((puVar8 != (uint *)0x0) && ((*puVar8 >> 0x16 & 1) == 0)) {
                *puVar8 = *puVar8 | 0x40400000;
              }
              iVar7 = iVar4 + 0x6c;
              _opd_FUN_002438b8(*(undefined4 *)(iVar4 + 0xc1a78));
              uVar5 = *(undefined4 *)(iVar4 + 0xc1a30);
              _opd_FUN_00aa960c(iVar7);
              uVar16 = *(undefined4 *)(iVar4 + 0xc1aa8);
              uVar9 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fdc));
              uVar10 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fd8));
              uVar11 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fd4));
              uVar12 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fd0));
              uVar13 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fcc));
              uVar14 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fc8));
              _opd_FUN_00aa9268(iVar7,uVar16,uVar9,uVar10,uVar11,uVar12,uVar13,uVar14);
              uVar16 = *(undefined4 *)(puVar2 + -0x7fc4);
              uVar9 = *(undefined4 *)(iVar4 + 0xc1a7c);
              uVar12 = _opd_FUN_000cdc90(uVar16);
              uVar10 = *(undefined4 *)(iVar4 + 0xc1a78);
              uVar13 = _opd_FUN_000cdc90(uVar16);
              uVar16 = *(undefined4 *)(puVar2 + -0x7fc0);
              uVar14 = _opd_FUN_000cdc90(uVar16);
              uVar11 = *(undefined4 *)(puVar2 + -0x7fbc);
              uVar15 = _opd_FUN_000cdc90(uVar11);
              _opd_FUN_00aa8f7c(iVar7,uVar9,uVar10,uVar12,uVar10,uVar13,uVar14,uVar15);
              uVar9 = *(undefined4 *)(puVar2 + -0x7fb8);
              uVar10 = *(undefined4 *)(iVar4 + 0xc1a80);
              uVar13 = _opd_FUN_000cdc90(uVar9);
              uVar12 = *(undefined4 *)(iVar4 + 0xc1a78);
              uVar9 = _opd_FUN_000cdc90(uVar9);
              uVar14 = _opd_FUN_000cdc90(uVar16);
              uVar15 = _opd_FUN_000cdc90(uVar11);
              _opd_FUN_00aa8f7c(iVar7,uVar10,uVar12,uVar13,uVar12,uVar9,uVar14,uVar15);
              uVar9 = *(undefined4 *)(puVar2 + -0x7fb4);
              uVar10 = *(undefined4 *)(iVar4 + 0xc1a84);
              uVar13 = _opd_FUN_000cdc90(uVar9);
              uVar12 = *(undefined4 *)(iVar4 + 0xc1a78);
              uVar9 = _opd_FUN_000cdc90(uVar9);
              uVar14 = _opd_FUN_000cdc90(uVar16);
              uVar15 = _opd_FUN_000cdc90(uVar11);
              _opd_FUN_00aa8f7c(iVar7,uVar10,uVar12,uVar13,uVar12,uVar9,uVar14,uVar15);
              uVar9 = *(undefined4 *)(puVar2 + -0x7fb0);
              uVar10 = *(undefined4 *)(iVar4 + 0xc1a88);
              uVar13 = _opd_FUN_000cdc90(uVar9);
              uVar12 = *(undefined4 *)(iVar4 + 0xc1a78);
              uVar9 = _opd_FUN_000cdc90(uVar9);
              uVar14 = _opd_FUN_000cdc90(uVar16);
              uVar15 = _opd_FUN_000cdc90(uVar11);
              _opd_FUN_00aa8f7c(iVar7,uVar10,uVar12,uVar13,uVar12,uVar9,uVar14,uVar15);
              uVar9 = *(undefined4 *)(puVar2 + -0x7fac);
              uVar10 = *(undefined4 *)(iVar4 + 0xc1a8c);
              uVar13 = _opd_FUN_000cdc90(uVar9);
              uVar12 = *(undefined4 *)(iVar4 + 0xc1a78);
              uVar9 = _opd_FUN_000cdc90(uVar9);
              uVar14 = _opd_FUN_000cdc90(uVar16);
              uVar15 = _opd_FUN_000cdc90(uVar11);
              _opd_FUN_00aa8f7c(iVar7,uVar10,uVar12,uVar13,uVar12,uVar9,uVar14,uVar15);
              uVar9 = *(undefined4 *)(puVar2 + -0x7fa8);
              uVar10 = *(undefined4 *)(iVar4 + 0xc1a90);
              uVar13 = _opd_FUN_000cdc90(uVar9);
              uVar12 = *(undefined4 *)(iVar4 + 0xc1a78);
              uVar9 = _opd_FUN_000cdc90(uVar9);
              uVar14 = _opd_FUN_000cdc90(uVar16);
              uVar15 = _opd_FUN_000cdc90(uVar11);
              _opd_FUN_00aa8f7c(iVar7,uVar10,uVar12,uVar13,uVar12,uVar9,uVar14,uVar15);
              uVar9 = *(undefined4 *)(puVar2 + -0x7fa4);
              uVar10 = *(undefined4 *)(iVar4 + 0xc1a94);
              uVar13 = _opd_FUN_000cdc90(uVar9);
              uVar12 = *(undefined4 *)(iVar4 + 0xc1a78);
              uVar9 = _opd_FUN_000cdc90(uVar9);
              uVar14 = _opd_FUN_000cdc90(uVar16);
              uVar15 = _opd_FUN_000cdc90(uVar11);
              _opd_FUN_00aa8f7c(iVar7,uVar10,uVar12,uVar13,uVar12,uVar9,uVar14,uVar15);
              uVar9 = *(undefined4 *)(puVar2 + -0x7fa0);
              uVar10 = *(undefined4 *)(iVar4 + 0xc1a98);
              uVar13 = _opd_FUN_000cdc90(uVar9);
              uVar12 = *(undefined4 *)(iVar4 + 0xc1a78);
              uVar9 = _opd_FUN_000cdc90(uVar9);
              uVar14 = _opd_FUN_000cdc90(uVar16);
              uVar15 = _opd_FUN_000cdc90(uVar11);
              _opd_FUN_00aa8f7c(iVar7,uVar10,uVar12,uVar13,uVar12,uVar9,uVar14,uVar15);
              uVar9 = *(undefined4 *)(puVar2 + -0x7f9c);
              uVar10 = *(undefined4 *)(iVar4 + 0xc1a9c);
              uVar13 = _opd_FUN_000cdc90(uVar9);
              uVar12 = *(undefined4 *)(iVar4 + 0xc1a78);
              uVar9 = _opd_FUN_000cdc90(uVar9);
              uVar14 = _opd_FUN_000cdc90(uVar16);
              uVar15 = _opd_FUN_000cdc90(uVar11);
              _opd_FUN_00aa8f7c(iVar7,uVar10,uVar12,uVar13,uVar12,uVar9,uVar14,uVar15);
              uVar9 = *(undefined4 *)(puVar2 + -0x7f98);
              uVar10 = *(undefined4 *)(iVar4 + 0xc1aa0);
              uVar13 = _opd_FUN_000cdc90(uVar9);
              uVar12 = *(undefined4 *)(iVar4 + 0xc1a78);
              uVar9 = _opd_FUN_000cdc90(uVar9);
              uVar14 = _opd_FUN_000cdc90(uVar16);
              uVar15 = _opd_FUN_000cdc90(uVar11);
              _opd_FUN_00aa8f7c(iVar7,uVar10,uVar12,uVar13,uVar12,uVar9,uVar14,uVar15);
              uVar9 = *(undefined4 *)(puVar2 + -0x7f94);
              uVar10 = *(undefined4 *)(iVar4 + 0xc1aa4);
              uVar13 = _opd_FUN_000cdc90(uVar9);
              uVar12 = *(undefined4 *)(iVar4 + 0xc1a78);
              uVar9 = _opd_FUN_000cdc90(uVar9);
              uVar16 = _opd_FUN_000cdc90(uVar16);
              uVar11 = _opd_FUN_000cdc90(uVar11);
              _opd_FUN_00aa8f7c(iVar7,uVar10,uVar12,uVar13,uVar12,uVar9,uVar16,uVar11);
              uVar16 = _opd_FUN_00a0c930(0x458);
              uVar9 = _opd_FUN_00a0c930(0x459);
              _opd_FUN_00aa8920(iVar7,uVar16,uVar9,*(undefined4 *)(puVar2 + -0x7f90));
              *(undefined4 *)(iVar7 + *(int *)(iVar4 + 0xc1a38) * 0x318 + -8) = 0x1d;
              uVar16 = _opd_FUN_00a0c930(0x4ae);
              uVar9 = _opd_FUN_00a0c930(0x4af);
              _opd_FUN_00aa8920(iVar7,uVar16,uVar9,*(undefined4 *)(puVar2 + -0x7f8c));
              piVar18 = *(int **)(puVar2 + -0x7ff0);
              *(undefined4 *)(iVar7 + *(int *)(iVar4 + 0xc1a38) * 0x318 + -8) = 0x1e;
              iVar6 = _opd_FUN_00f1719c(*(undefined4 *)(*piVar18 + 0x60));
              if ((((iVar6 == 0) &&
                   (iVar6 = _opd_FUN_0097c124(), *(char *)(iVar6 + 0x2b4) != '\x04')) &&
                  (iVar6 = _opd_FUN_0097c124(), *(char *)(iVar6 + 0x2b4) != '\n')) &&
                 (iVar6 = _opd_FUN_0097c124(), *(char *)(iVar6 + 0x2b4) != '\x03')) {
                cVar17 = _opd_FUN_00f04238(*(undefined4 *)(*piVar18 + 0x60),2);
                if ((cVar17 != '\0') &&
                   (cVar17 = _opd_FUN_00f04238(*(undefined4 *)(*piVar18 + 0x60),0x15),
                   cVar17 == '\0')) {
                  uVar16 = _opd_FUN_00a0c930(0x44f);
                  uVar9 = _opd_FUN_00a0c930(0x451);
                  _opd_FUN_00aa8920(iVar7,uVar16,uVar9,*(undefined4 *)(puVar2 + -0x7f88));
                  *(undefined4 *)(iVar7 + *(int *)(iVar4 + 0xc1a38) * 0x318 + -0x10) = 2;
                  *(undefined4 *)(iVar7 + *(int *)(iVar4 + 0xc1a38) * 0x318 + -8) = 0x1f;
                }
                uVar9 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7f84));
                uVar16 = *(undefined4 *)(puVar2 + -0x7f80);
                uVar10 = _opd_FUN_000cdc90(uVar16);
                uVar9 = _opd_FUN_0023e4a0(uVar9,uVar10);
                uVar10 = _opd_FUN_00a0c930(0x455);
                _opd_FUN_00aa8920(iVar7,uVar9,uVar10,*(undefined4 *)(puVar2 + -0x7f7c));
                uVar9 = _opd_FUN_00a0c930(0x474);
                uVar10 = _opd_FUN_00a0c930(0x475);
                _opd_FUN_00aa8920(iVar7,uVar9,uVar10,*(undefined4 *)(puVar2 + -0x7f78));
                *(undefined4 *)(iVar7 + *(int *)(iVar4 + 0xc1a38) * 0x318 + -8) = 0x20;
                uVar9 = _opd_FUN_00a0c930(0x47e);
                uVar10 = _opd_FUN_00a0c930(0x47f);
                _opd_FUN_00aa8920(iVar7,uVar9,uVar10,*(undefined4 *)(puVar2 + -0x7f74));
                uVar9 = *(undefined4 *)(puVar2 + -0x7f70);
                *(undefined4 *)(iVar7 + *(int *)(iVar4 + 0xc1a38) * 0x318 + -8) = 0x21;
                uVar10 = _opd_FUN_000cdc90(uVar9);
                uVar16 = _opd_FUN_000cdc90(uVar16);
                uVar16 = _opd_FUN_0023e4a0(uVar10,uVar16);
                uVar9 = _opd_FUN_000cdc90(uVar9);
                uVar10 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7f6c));
                uVar9 = _opd_FUN_0023e4a0(uVar9,uVar10);
                _opd_FUN_00aa8920(iVar7,uVar16,uVar9,*(undefined4 *)(puVar2 + -0x7f68));
                uVar16 = _opd_FUN_00a0c930(0x484);
                uVar9 = _opd_FUN_00a0c930(0x485);
                _opd_FUN_00aa8920(iVar7,uVar16,uVar9,*(undefined4 *)(puVar2 + -0x7f64));
                *(undefined4 *)(iVar7 + *(int *)(iVar4 + 0xc1a38) * 0x318 + -0x10) = 1;
              }
              uVar16 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7f60));
              uVar9 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7f80));
              uVar16 = _opd_FUN_0023e4a0(uVar16,uVar9);
              uVar9 = _opd_FUN_00a0c930(0x4a6);
              _opd_FUN_00aa8920(iVar7,uVar16,uVar9,*(undefined4 *)(puVar2 + -0x7f5c));
              iVar6 = _opd_FUN_00f1719c(*(undefined4 *)(**(int **)(puVar2 + -0x7ff0) + 0x60));
              if (((iVar6 == 0) && (iVar6 = _opd_FUN_0097c124(), *(char *)(iVar6 + 0x2b4) != '\x04')
                  ) && ((iVar6 = _opd_FUN_0097c124(), *(char *)(iVar6 + 0x2b4) != '\n' &&
                        (iVar6 = _opd_FUN_0097c124(), *(char *)(iVar6 + 0x2b4) != '\x03')))) {
                uVar16 = _opd_FUN_00a0c930(0x4a9);
                uVar9 = _opd_FUN_00a0c930(0x4ab);
                _opd_FUN_00aa8920(iVar7,uVar16,uVar9,*(undefined4 *)(puVar2 + -0x7f58));
                *(undefined4 *)(iVar7 + *(int *)(iVar4 + 0xc1a38) * 0x318 + -8) = 0x24;
              }
              _opd_FUN_00aaacc8(iVar7,uVar5);
              iVar6 = *(int *)(iVar7 + *(int *)(iVar4 + 0xc1a30) * 0x318 + 0x308);
              if (iVar6 == 1) {
                uVar16 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7f4c));
                uVar5 = *(undefined4 *)(puVar2 + -0x7f48);
              }
              else if (iVar6 == 2) {
                uVar16 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7f54));
                uVar5 = *(undefined4 *)(puVar2 + -0x7f50);
              }
              else {
                uVar16 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7f44));
                uVar5 = *(undefined4 *)(puVar2 + -0x7fdc);
              }
              uVar5 = _opd_FUN_000cdc90(uVar5);
              _opd_FUN_00aa81dc(iVar7,uVar16,uVar5);
              _opd_FUN_00a0ca3c(*(undefined4 *)(iVar4 + 0xc1a78),0x698386,
                                *(int *)(iVar4 + 0xc1a30) * 0x318 + iVar7 + 0x181);
              if (*(longlong *)(iVar4 + 0xc1a70) == 0) {
                uVar19 = _opd_FUN_009f7cc8(*(undefined4 *)(iVar4 + 0xc1a2c),0x10c2d1);
                *(undefined8 *)(iVar4 + 0xc1a70) = uVar19;
                _opd_FUN_000beea8(iVar4,uVar19);
              }
              if ((*(uint *)(iVar7 + *(int *)(iVar4 + 0xc1a30) * 0x318 + 0x308) >> 1 & 1) == 0) {
                _opd_FUN_00a88c18();
              }
              else {
                _opd_FUN_00a88ca8();
              }
              if (*(int *)(iVar4 + 0xc1a78) != 0) {
                _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7f3c),*(int *)(iVar4 + 0xc1a78));
              }
              iVar6 = 0;
              piVar18 = (int *)(iVar4 + 0xc1a7c);
              do {
                if (*piVar18 != 0) {
                  _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7f3c),*piVar18);
                }
                bVar1 = iVar6 != 0x28;
                iVar6 = iVar6 + 4;
                piVar18 = piVar18 + 1;
              } while (bVar1);
              if (*(int *)(iVar4 + 0xc1aa8) != 0) {
                _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7f3c),*(int *)(iVar4 + 0xc1aa8));
              }
              if (*(int *)(iVar4 + 0xc1aac) != 0) {
                _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7f3c),*(int *)(iVar4 + 0xc1aac));
              }
              if (*(int *)(iVar4 + 0xc1ab0) != 0) {
                _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7f3c),*(int *)(iVar4 + 0xc1ab0));
              }
              _opd_FUN_00a2ddf4();
              _opd_FUN_000482b0(0x499);
              *(undefined2 *)(**(int **)(puVar2 + -0x7ff0) + 100) = 0;
              return lVar3;
            }
          }
        }
      }
    }
LAB_00a02510:
    _opd_FUN_000c1e00(iVar4);
  }
  return 0;
}


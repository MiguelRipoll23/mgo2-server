// hook SITE 0x009b9fb8

longlong _opd_FUN_009b970c(int param_1,undefined4 *param_2,undefined2 param_3,undefined1 param_4)

{
  bool bVar1;
  undefined *puVar2;
  undefined2 uVar3;
  int iVar5;
  longlong lVar4;
  undefined4 uVar6;
  int iVar7;
  undefined4 uVar8;
  uint *puVar9;
  int iVar10;
  undefined4 uVar11;
  undefined4 uVar12;
  undefined8 uVar13;
  int *piVar14;
  undefined *local_e0 [32];
  
  puVar2 = PTR_PTR_0121bb10;
  uVar13 = *(undefined8 *)(PTR_PTR_0121bb10 + -0x7ff0);
  iVar5 = _opd_FUN_000be778(0x586df0,uVar13);
  if (iVar5 == 0) {
    return 0;
  }
  lVar4 = _opd_FUN_000bead8(iVar5,uVar13,*(undefined4 *)(puVar2 + -0x7fe8),
                            *(undefined4 *)(puVar2 + -0x7fe4));
  if (lVar4 == 0) goto LAB_009ba6bc;
  piVar14 = *(int **)(puVar2 + -0x8000);
  *piVar14 = iVar5;
  if ((param_2 == (undefined4 *)0x0) || (param_1 == 0)) {
    _opd_FUN_0097f1b8(0xf08,0xffffffffffffffe8,0);
    iVar5 = *piVar14;
    uVar3 = 0x10;
LAB_009ba6b4:
    *(undefined2 *)(iVar5 + 100) = uVar3;
  }
  else {
    *(undefined4 **)(iVar5 + 0x6c) = param_2;
    *(int *)(iVar5 + 0x68) = param_1;
    *param_2 = 0xffffffff;
    *(undefined2 *)(iVar5 + 0x70) = param_3;
    *(undefined1 *)(iVar5 + 0x72) = param_4;
    uVar6 = _opd_FUN_00280418();
    iVar10 = 0;
    *(undefined4 *)(iVar5 + 0x60) = uVar6;
    uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fe0));
    piVar14 = (int *)(iVar5 + 0x586d50);
    do {
      iVar7 = _opd_FUN_00242400(uVar6,1,0,0x3b);
      *piVar14 = iVar7;
      if (iVar7 == 0) goto LAB_009ba6bc;
      uVar8 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7ffc));
      puVar9 = (uint *)_opd_FUN_00242bf0(iVar7,uVar8);
      bVar1 = iVar10 != 4;
      iVar10 = iVar10 + 4;
      if ((puVar9 != (uint *)0x0) && ((*puVar9 >> 0x16 & 1) != 0)) {
        *puVar9 = *puVar9 & 0xffbfffff | 0x40000000;
      }
      piVar14 = piVar14 + 1;
    } while (bVar1);
    iVar10 = 0;
    uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fdc));
    piVar14 = (int *)(iVar5 + 0x586d58);
    do {
      iVar7 = _opd_FUN_00242400(uVar6,1,0,0x50);
      *piVar14 = iVar7;
      if (iVar7 == 0) goto LAB_009ba6bc;
      uVar8 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7ffc));
      puVar9 = (uint *)_opd_FUN_00242bf0(iVar7,uVar8);
      bVar1 = iVar10 != 0x18;
      iVar10 = iVar10 + 4;
      if ((puVar9 != (uint *)0x0) && ((*puVar9 >> 0x16 & 1) != 0)) {
        *puVar9 = *puVar9 & 0xffbfffff | 0x40000000;
      }
      piVar14 = piVar14 + 1;
    } while (bVar1);
    uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fd8));
    iVar10 = _opd_FUN_00242400(uVar6,1,0,0x50);
    *(int *)(iVar5 + 0x586d74) = iVar10;
    if (iVar10 != 0) {
      uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7ffc));
      puVar9 = (uint *)_opd_FUN_00242bf0(iVar10,uVar6);
      if ((puVar9 != (uint *)0x0) && ((*puVar9 >> 0x16 & 1) != 0)) {
        *puVar9 = *puVar9 & 0xffbfffff | 0x40000000;
      }
      iVar10 = 0;
      uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fd4));
      piVar14 = (int *)(iVar5 + 0x586dd0);
      do {
        iVar7 = _opd_FUN_00242400(uVar6,1,0,0x46);
        *piVar14 = iVar7;
        if (iVar7 == 0) goto LAB_009ba6bc;
        uVar8 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7ffc));
        puVar9 = (uint *)_opd_FUN_00242bf0(iVar7,uVar8);
        bVar1 = iVar10 != 4;
        iVar10 = iVar10 + 4;
        if ((puVar9 != (uint *)0x0) && ((*puVar9 >> 0x16 & 1) != 0)) {
          *puVar9 = *puVar9 & 0xffbfffff | 0x40000000;
        }
        piVar14 = piVar14 + 1;
      } while (bVar1);
      uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fd0));
      iVar10 = _opd_FUN_00242400(uVar6,1,0,0x46);
      *(int *)(iVar5 + 0x586da4) = iVar10;
      if (iVar10 != 0) {
        uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7ffc));
        puVar9 = (uint *)_opd_FUN_00242bf0(iVar10,uVar6);
        if ((puVar9 != (uint *)0x0) && ((*puVar9 >> 0x16 & 1) != 0)) {
          *puVar9 = *puVar9 & 0xffbfffff | 0x40000000;
        }
        puVar9 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar5 + 0x586da4),0x2a4634);
        if ((puVar9 != (uint *)0x0) && ((*puVar9 >> 0x16 & 1) != 0)) {
          *puVar9 = *puVar9 & 0xffbfffff | 0x40000000;
        }
        uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fd0));
        iVar10 = _opd_FUN_00242400(uVar6,1,0,0x46);
        *(int *)(iVar5 + 0x586da8) = iVar10;
        if (iVar10 != 0) {
          uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7ffc));
          puVar9 = (uint *)_opd_FUN_00242bf0(iVar10,uVar6);
          if ((puVar9 != (uint *)0x0) && ((*puVar9 >> 0x16 & 1) != 0)) {
            *puVar9 = *puVar9 & 0xffbfffff | 0x40000000;
          }
          uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fdc));
          iVar10 = _opd_FUN_00242400(uVar6,1,0,0x46);
          *(int *)(iVar5 + 0x586d78) = iVar10;
          if (iVar10 != 0) {
            uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7ffc));
            puVar9 = (uint *)_opd_FUN_00242bf0(iVar10,uVar6);
            if ((puVar9 != (uint *)0x0) && ((*puVar9 >> 0x16 & 1) != 0)) {
              *puVar9 = *puVar9 & 0xffbfffff | 0x40000000;
            }
            uVar6 = *(undefined4 *)(iVar5 + 0x586d78);
            uVar8 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fcc));
            _opd_FUN_0024ae50(uVar6,uVar8);
            puVar9 = (uint *)_opd_FUN_00242bf0(*(undefined4 *)(iVar5 + 0x586d78),0x2a4634);
            if ((puVar9 != (uint *)0x0) && ((*puVar9 >> 0x16 & 1) != 0)) {
              *puVar9 = *puVar9 & 0xffbfffff | 0x40000000;
            }
            iVar10 = 0;
            uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fd8));
            piVar14 = (int *)(iVar5 + 0x586dac);
            do {
              iVar7 = _opd_FUN_00242400(uVar6,1,0,0x47);
              *piVar14 = iVar7;
              if (iVar7 == 0) goto LAB_009ba6bc;
              uVar8 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7ffc));
              puVar9 = (uint *)_opd_FUN_00242bf0(iVar7,uVar8);
              bVar1 = iVar10 != 8;
              iVar10 = iVar10 + 4;
              if ((puVar9 != (uint *)0x0) && ((*puVar9 >> 0x16 & 1) != 0)) {
                *puVar9 = *puVar9 & 0xffbfffff | 0x40000000;
              }
              piVar14 = piVar14 + 1;
            } while (bVar1);
            uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fc8));
            iVar10 = _opd_FUN_00242400(uVar6,1,0,0x46);
            *(int *)(iVar5 + 0x586db8) = iVar10;
            if (iVar10 != 0) {
              uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7ffc));
              puVar9 = (uint *)_opd_FUN_00242bf0(iVar10,uVar6);
              if ((puVar9 != (uint *)0x0) && ((*puVar9 >> 0x16 & 1) != 0)) {
                *puVar9 = *puVar9 & 0xffbfffff | 0x40000000;
              }
              uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fd0));
              iVar10 = _opd_FUN_00242400(uVar6,1,0,0x46);
              *(int *)(iVar5 + 0x586dbc) = iVar10;
              if (iVar10 != 0) {
                uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7ffc));
                puVar9 = (uint *)_opd_FUN_00242bf0(iVar10,uVar6);
                if ((puVar9 != (uint *)0x0) && ((*puVar9 >> 0x16 & 1) != 0)) {
                  *puVar9 = *puVar9 & 0xffbfffff | 0x40000000;
                }
                iVar10 = 0;
                uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fd8));
                piVar14 = (int *)(iVar5 + 0x586dc0);
                do {
                  iVar7 = _opd_FUN_00242400(uVar6,1,0,0x46);
                  *piVar14 = iVar7;
                  if (iVar7 == 0) goto LAB_009ba6bc;
                  uVar8 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7ffc));
                  puVar9 = (uint *)_opd_FUN_00242bf0(iVar7,uVar8);
                  bVar1 = iVar10 != 0xc;
                  iVar10 = iVar10 + 4;
                  if ((puVar9 != (uint *)0x0) && ((*puVar9 >> 0x16 & 1) != 0)) {
                    *puVar9 = *puVar9 & 0xffbfffff | 0x40000000;
                  }
                  piVar14 = piVar14 + 1;
                } while (bVar1);
                iVar10 = 0;
                uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fc4));
                piVar14 = (int *)(iVar5 + 0x586d7c);
                do {
                  iVar7 = _opd_FUN_00242400(uVar6,1,0,0x46);
                  *piVar14 = iVar7;
                  if (iVar7 == 0) goto LAB_009ba6bc;
                  uVar8 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7ffc));
                  puVar9 = (uint *)_opd_FUN_00242bf0(iVar7,uVar8);
                  bVar1 = iVar10 != 0x24;
                  iVar10 = iVar10 + 4;
                  if ((puVar9 != (uint *)0x0) && ((*puVar9 >> 0x16 & 1) != 0)) {
                    *puVar9 = *puVar9 & 0xffbfffff | 0x40000000;
                  }
                  piVar14 = piVar14 + 1;
                } while (bVar1);
                uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fc0));
                iVar10 = _opd_FUN_00242358(uVar6,1,0,0x46,0x20);
                *(int *)(iVar5 + 0x586dd8) = iVar10;
                if (iVar10 != 0) {
                  uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7ffc));
                  puVar9 = (uint *)_opd_FUN_00242bf0(iVar10,uVar6);
                  if ((puVar9 != (uint *)0x0) && ((*puVar9 >> 0x16 & 1) != 0)) {
                    *puVar9 = *puVar9 & 0xffbfffff | 0x40000000;
                  }
                  _opd_FUN_00fa4d48(local_e0,0,0x80);
                  iVar10 = 0;
                  local_e0[0] = &DAT_008c1b88;
                  local_e0[1] = (undefined *)0x70e735;
                  local_e0[2] = (undefined *)0x70e737;
                  local_e0[3] = (undefined *)0x65efbb;
                  local_e0[4] = (undefined *)0x70e73b;
                  local_e0[5] = (undefined *)0x70e73d;
                  local_e0[6] = (undefined *)0x65efc1;
                  local_e0[7] = (undefined *)0x65efc3;
                  local_e0[8] = (undefined *)0x65efff;
                  local_e0[9] = (undefined *)0x65ef73;
                  local_e0[10] = (undefined *)0x65f035;
                  local_e0[0xb] = (undefined *)0x65f001;
                  local_e0[0xc] = (undefined *)0x65effd;
                  local_e0[0xd] = (undefined *)0x65eff9;
                  local_e0[0xe] = (undefined *)0x65f037;
                  local_e0[0xf] = (undefined *)0x65ef85;
                  do {
                    if (*(int *)((int)local_e0 + iVar10) == 0) break;
                    uVar6 = _opd_FUN_00256af0(*(undefined4 *)(iVar5 + 0x586dd8),
                                              *(int *)((int)local_e0 + iVar10),0x6fba98);
                    iVar7 = iVar5 + iVar10;
                    bVar1 = iVar10 != 0x7c;
                    iVar10 = iVar10 + 4;
                    *(undefined4 *)(iVar7 + 0x74) = uVar6;
                  } while (bVar1);
                  uVar6 = _opd_FUN_000cdc90(*(undefined4 *)(puVar2 + -0x7fbc));
                  iVar10 = _opd_FUN_00242400(uVar6,1,0,0x46);
                  *(int *)(iVar5 + 0x586ddc) = iVar10;
                  if (iVar10 != 0) {
                    _opd_FUN_002438e0(iVar10);
                    uVar6 = *(undefined4 *)(puVar2 + -0x7fb8);
                    uVar8 = _opd_FUN_000cdc90(uVar6);
                    iVar10 = _opd_FUN_00242400(uVar8,1,0,0xdc);
                    *(int *)(iVar5 + 0x586de0) = iVar10;
                    if (iVar10 != 0) {
                      uVar11 = _opd_FUN_00ead010(iVar10,0xee77d1);
                      uVar8 = *(undefined4 *)(iVar5 + 0x586de0);
                      uVar12 = _opd_FUN_00a0c930(0x4a1);
                      _opd_FUN_00eacf10(uVar8,uVar11,uVar12);
                      uVar6 = _opd_FUN_000cdc90(uVar6);
                      iVar10 = _opd_FUN_00242400(uVar6,1,0,0xdc);
                      *(int *)(iVar5 + 0x586de4) = iVar10;
                      if (iVar10 != 0) {
                        uVar8 = _opd_FUN_00ead010(iVar10,0xee77d1);
                        uVar6 = *(undefined4 *)(iVar5 + 0x586de4);
                        uVar11 = _opd_FUN_00a0c930(0x1c7);
                        _opd_FUN_00eacf10(uVar6,uVar8,uVar11);
                        iVar10 = _opd_FUN_0097c124();
                        _opd_FUN_00a0ed68(*(undefined4 *)(iVar10 + 0x2a4),0x1b4,0x1b5);
                        iVar7 = _opd_FUN_0097c124();
                        uVar6 = *(undefined4 *)(iVar7 + 0x2a4);
                        uVar8 = _opd_FUN_009745d0(*(undefined4 *)(puVar2 + -0x7ff8));
                        _opd_FUN_00a0ca3c(uVar6,0x69838a,uVar8);
                        iVar7 = _opd_FUN_0097c124();
                        _opd_FUN_0097e020(*(undefined4 *)(iVar7 + 0x2a4),0,0,0);
                        uVar8 = _opd_FUN_00ead010(*(undefined4 *)(iVar10 + 0x2a4),0xbd9e07);
                        uVar6 = *(undefined4 *)(iVar10 + 0x2a4);
                        uVar11 = _opd_FUN_00a0c930(0xb);
                        _opd_FUN_00ead038(uVar6,uVar8,uVar11);
                        uVar8 = _opd_FUN_00ead010(*(undefined4 *)(iVar10 + 0x2a4),0x9933cd);
                        uVar6 = *(undefined4 *)(iVar10 + 0x2a4);
                        uVar11 = _opd_FUN_00a0c930(0x1b4);
                        _opd_FUN_00ead038(uVar6,uVar8,uVar11);
                        uVar8 = _opd_FUN_00ead010(*(undefined4 *)(iVar10 + 0x2a4),0xf60b20);
                        uVar6 = *(undefined4 *)(iVar10 + 0x2a4);
                        uVar11 = _opd_FUN_00a0c930(0x45);
                        _opd_FUN_00ead038(uVar6,uVar8,uVar11);
                        uVar8 = _opd_FUN_00ead010(*(undefined4 *)(iVar10 + 0x2a4),0xdf99da);
                        uVar6 = *(undefined4 *)(iVar10 + 0x2a4);
                        uVar11 = _opd_FUN_00a0c930(0x20);
                        _opd_FUN_00ead038(uVar6,uVar8,uVar11);
                        uVar6 = _opd_FUN_00ead010(*(undefined4 *)(iVar10 + 0x2a4),0xeb4ea9);
                        iVar7 = _opd_FUN_0097c124();
                        if ((*(char *)(iVar7 + 0x2b4) == '\a') ||
                           (iVar7 = _opd_FUN_0097c124(), *(char *)(iVar7 + 0x2b4) == '\b')) {
LAB_009ba3a8:
                          uVar13 = 3;
                        }
                        else {
                          iVar7 = _opd_FUN_0097c124();
                          uVar13 = 0x2b;
                          if (*(char *)(iVar7 + 0x2b4) == '\t') goto LAB_009ba3a8;
                        }
                        uVar8 = *(undefined4 *)(iVar10 + 0x2a4);
                        uVar11 = _opd_FUN_00a0c930(uVar13);
                        iVar7 = 0;
                        _opd_FUN_00ead038(uVar8,uVar6,uVar11);
                        uVar8 = _opd_FUN_00ead010(*(undefined4 *)(iVar10 + 0x2a4),0x2107e8);
                        uVar6 = *(undefined4 *)(iVar10 + 0x2a4);
                        uVar11 = _opd_FUN_00a0c930(0x13f);
                        _opd_FUN_00ead038(uVar6,uVar8,uVar11);
                        piVar14 = (int *)(iVar5 + 0x586d50);
                        do {
                          if (*piVar14 != 0) {
                            _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7fb4),*piVar14);
                          }
                          bVar1 = iVar7 != 4;
                          iVar7 = iVar7 + 4;
                          piVar14 = piVar14 + 1;
                        } while (bVar1);
                        iVar10 = 0;
                        piVar14 = (int *)(iVar5 + 0x586d58);
                        do {
                          if (*piVar14 != 0) {
                            _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7fb4),*piVar14);
                          }
                          bVar1 = iVar10 != 0x18;
                          iVar10 = iVar10 + 4;
                          piVar14 = piVar14 + 1;
                        } while (bVar1);
                        if (*(int *)(iVar5 + 0x586d74) != 0) {
                          _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7fb4),
                                            *(int *)(iVar5 + 0x586d74));
                        }
                        if (*(int *)(iVar5 + 0x586d78) != 0) {
                          _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7fb4),
                                            *(int *)(iVar5 + 0x586d78));
                        }
                        iVar10 = 0;
                        piVar14 = (int *)(iVar5 + 0x586d7c);
                        do {
                          if (*piVar14 != 0) {
                            _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7fb4),*piVar14);
                          }
                          bVar1 = iVar10 != 0x24;
                          iVar10 = iVar10 + 4;
                          piVar14 = piVar14 + 1;
                        } while (bVar1);
                        if (*(int *)(iVar5 + 0x586da4) != 0) {
                          _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7fb4),
                                            *(int *)(iVar5 + 0x586da4));
                        }
                        if (*(int *)(iVar5 + 0x586da8) != 0) {
                          _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7fb4),
                                            *(int *)(iVar5 + 0x586da8));
                        }
                        iVar10 = 0;
                        piVar14 = (int *)(iVar5 + 0x586dac);
                        do {
                          if (*piVar14 != 0) {
                            _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7fb4),*piVar14);
                          }
                          bVar1 = iVar10 != 8;
                          iVar10 = iVar10 + 4;
                          piVar14 = piVar14 + 1;
                        } while (bVar1);
                        if (*(int *)(iVar5 + 0x586db8) != 0) {
                          _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7fb4),
                                            *(int *)(iVar5 + 0x586db8));
                        }
                        if (*(int *)(iVar5 + 0x586dbc) != 0) {
                          _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7fb4),
                                            *(int *)(iVar5 + 0x586dbc));
                        }
                        iVar10 = 0;
                        piVar14 = (int *)(iVar5 + 0x586dc0);
                        do {
                          if (*piVar14 != 0) {
                            _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7fb4),*piVar14);
                          }
                          bVar1 = iVar10 != 0xc;
                          iVar10 = iVar10 + 4;
                          piVar14 = piVar14 + 1;
                        } while (bVar1);
                        iVar10 = 0;
                        piVar14 = (int *)(iVar5 + 0x586dd0);
                        do {
                          if (*piVar14 != 0) {
                            _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7fb4),*piVar14);
                          }
                          bVar1 = iVar10 != 4;
                          iVar10 = iVar10 + 4;
                          piVar14 = piVar14 + 1;
                        } while (bVar1);
                        if (*(int *)(iVar5 + 0x586dd8) != 0) {
                          _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7fb4),
                                            *(int *)(iVar5 + 0x586dd8));
                        }
                        if (*(int *)(iVar5 + 0x586ddc) != 0) {
                          _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7fb4),
                                            *(int *)(iVar5 + 0x586ddc));
                        }
                        if (*(int *)(iVar5 + 0x586de0) != 0) {
                          _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7fb4),
                                            *(int *)(iVar5 + 0x586de0));
                        }
                        if (*(int *)(iVar5 + 0x586de4) != 0) {
                          _opd_FUN_0024d078((double)*(float *)(puVar2 + -0x7fb4),
                                            *(int *)(iVar5 + 0x586de4));
                        }
                        uVar3 = 0;
                        iVar5 = **(int **)(puVar2 + -0x8000);
                        goto LAB_009ba6b4;
                      }
                    }
                  }
                }
              }
            }
          }
        }
      }
    }
LAB_009ba6bc:
    lVar4 = 0;
    _opd_FUN_000c1e00(iVar5);
  }
  return lVar4;
}


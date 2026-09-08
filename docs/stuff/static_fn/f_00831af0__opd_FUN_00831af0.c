/* containing function of static patch target(s); entry .opd.FUN_00831af0 @ 00831af0 */

void _opd_FUN_00831af0(int param_1)

{
  undefined2 uVar1;
  int iVar2;
  int iVar3;
  uint uVar4;
  undefined4 *puVar5;
  undefined4 uVar6;
  bool bVar7;
  undefined *puVar8;
  uint uVar9;
  char cVar12;
  int iVar10;
  int iVar11;
  
  puVar8 = PTR_DAT_0121b88c;
  iVar10 = *(int *)(param_1 + 0x1c);
  iVar11 = *(int *)(iVar10 + 0x18);
  iVar3 = *(int *)(iVar11 + 0xda0);
  for (iVar2 = *(int *)(iVar11 + 0x18);
      (bVar7 = iVar2 != 0, bVar7 && ((*(uint *)(iVar2 + 0x28) & 2) == 0));
      iVar2 = *(int *)(iVar2 + 0x14)) {
  }
  uVar9 = *(uint *)(iVar10 + 0x90);
  if ((int)uVar9 < 0x27) {
    if (0x24 < (int)uVar9) {
      uVar4 = *(uint *)(iVar10 + 0x828);
      if (uVar9 == uVar4) {
        return;
      }
      if (uVar4 != 0x24) {
        if ((int)uVar4 < 0x25) {
          if (0xd < (int)uVar4) {
            if (uVar4 == 0x1b) {
LAB_00832180:
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f80);
              *(undefined4 *)(param_1 + 0x44) = 0;
              return;
            }
            if ((int)uVar4 < 0x1c) {
              if (uVar4 == 0xf) goto LAB_00831f00;
              if (0xe < (int)uVar4) {
                if (uVar4 != 0x10) {
                  return;
                }
                goto LAB_00831bb4;
              }
            }
            else {
              if ((int)uVar4 < 0x21) {
                if ((int)uVar4 < 0x1f) {
                  if (uVar4 != 0x1c) {
                    return;
                  }
                  if (bVar7) {
                    return;
                  }
                  uVar6 = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f7c);
                  *(undefined4 *)(param_1 + 0x44) = 0;
                  *(undefined4 *)(param_1 + 0x40) = uVar6;
                  return;
                }
                goto LAB_00831f44;
              }
              if (uVar4 != 0x23) {
                return;
              }
            }
LAB_00832554:
            *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ecc);
            *(undefined4 *)(param_1 + 0x44) = 0;
            return;
          }
          if (0xb < (int)uVar4) {
LAB_00831bb4:
            *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ec4);
            *(ulonglong *)(param_1 + 0x140) = *(ulonglong *)(param_1 + 0x140) | 0x8000000000000000;
            *(undefined4 *)(param_1 + 0x44) = 0;
            return;
          }
          if ((int)uVar4 < 5) {
            if (2 < (int)uVar4) goto LAB_00831bb4;
            if (uVar4 == 1) goto LAB_00832554;
            if (uVar4 != 2) {
              return;
            }
          }
          else {
            if (uVar4 == 10) goto LAB_00832554;
            if (uVar4 != 0xb) {
              return;
            }
          }
        }
        else if ((int)uVar4 < 0x38) {
          if ((int)uVar4 < 0x35) {
            if (uVar4 != 0x2e) {
              if ((int)uVar4 < 0x2f) {
                if (uVar4 == 0x26) {
                  *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7eb8);
                  *(undefined4 *)(param_1 + 0x44) = 0;
                  return;
                }
                if ((int)uVar4 < 0x26) {
                  *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ebc);
                  *(undefined4 *)(param_1 + 0x44) = 0;
                  return;
                }
                if (uVar4 != 0x27) {
                  return;
                }
LAB_00831f44:
                *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7eb4);
                *(undefined4 *)(param_1 + 0x44) = 0;
                return;
              }
              if (uVar4 == 0x2f) goto LAB_00831f00;
              if ((int)uVar4 < 0x32) {
                return;
              }
            }
            goto LAB_00832554;
          }
        }
        else {
          if ((int)uVar4 < 0x3e) {
            if (0x3a < (int)uVar4) goto LAB_00832180;
            goto LAB_00831bb4;
          }
          if (uVar4 != 0x43) {
            if ((int)uVar4 < 0x44) {
              if (0x41 < (int)uVar4) {
                return;
              }
            }
            else if (uVar4 != 0x2ce) {
              return;
            }
            goto LAB_00832554;
          }
        }
      }
LAB_00831f00:
      *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ec8);
      *(undefined4 *)(param_1 + 0x44) = 0;
      return;
    }
    if (0x12 < (int)uVar9) {
      if (uVar9 == 0x1c) {
        iVar10 = *(int *)(iVar10 + 0x828);
        if (iVar10 == 0x1c) {
          return;
        }
        if (iVar10 < 0x1c4) {
          if (0x1c1 < iVar10) {
            return;
          }
          if (iVar10 == 0x199) {
            return;
          }
        }
        else if (iVar10 - 0x1c9U < 4) {
          return;
        }
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f80);
        *(undefined4 *)(param_1 + 0x44) = 0;
        return;
      }
      if (0x1c < (int)uVar9) {
        if (uVar9 == 0x20) {
          cVar12 = _opd_FUN_00333a38(*(undefined4 *)(iVar3 + 0x24),0,0,100);
          if ((cVar12 != '\0') && (*(int *)(iVar10 + 0x828) == 0x22)) {
            *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7eac);
            *(undefined4 *)(param_1 + 0x44) = 0;
            return;
          }
          cVar12 = _opd_FUN_00333608(*(undefined4 *)(iVar3 + 0x24),0,0);
          if (cVar12 == '\0') {
            return;
          }
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f54);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        if ((int)uVar9 < 0x21) {
          if (uVar9 == 0x1e) {
            cVar12 = _opd_FUN_00333a38(*(undefined4 *)(iVar3 + 0x24),0,0,100);
            if ((cVar12 != '\0') && (*(int *)(iVar10 + 0x828) == 0x22)) {
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7eac);
              *(undefined4 *)(param_1 + 0x44) = 0;
              return;
            }
            cVar12 = _opd_FUN_00333608(*(undefined4 *)(iVar3 + 0x24),0,0);
            if (cVar12 == '\0') {
              return;
            }
            *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f80);
            *(undefined4 *)(param_1 + 0x44) = 0;
            return;
          }
          if (0x1e < (int)uVar9) {
            cVar12 = _opd_FUN_00333a38(*(undefined4 *)(iVar3 + 0x24),0,0,100);
            if ((cVar12 != '\0') && (*(int *)(iVar10 + 0x828) == 0x21)) {
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7eb0);
              *(undefined4 *)(param_1 + 0x44) = 0;
              return;
            }
            cVar12 = _opd_FUN_00333608(*(undefined4 *)(iVar3 + 0x24),0,0);
            if (cVar12 == '\0') {
              return;
            }
            *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f54);
            *(undefined4 *)(param_1 + 0x44) = 0;
            return;
          }
          cVar12 = _opd_FUN_00333a38(*(undefined4 *)(iVar3 + 0x24),0,0,100);
          if ((cVar12 != '\0') && (*(int *)(iVar10 + 0x828) == 0x21)) {
            *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7eb0);
            *(undefined4 *)(param_1 + 0x44) = 0;
            return;
          }
          cVar12 = _opd_FUN_00333608(*(undefined4 *)(iVar3 + 0x24),0,0);
          if (cVar12 == '\0') {
            return;
          }
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f80);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        if (uVar9 == 0x22) {
          if (*(int *)(iVar10 + 0x828) == 0x22) {
            return;
          }
          iVar10 = *(int *)(param_1 + 0x150);
          if (iVar10 == 2) {
            cVar12 = _opd_FUN_00333a38(*(undefined4 *)(iVar3 + 0x24),0,0,0x91);
            if (cVar12 != '\0') {
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7ec0);
              *(undefined4 *)(param_1 + 0x44) = 0;
              *(undefined2 *)(*(int *)(iVar3 + 0x24) + 0x36e) = 0x8000;
              return;
            }
            iVar10 = *(int *)(param_1 + 0x150);
          }
          if (iVar10 != 3) {
            return;
          }
          cVar12 = _opd_FUN_00333608(*(undefined4 *)(iVar3 + 0x24),0,0);
          if (cVar12 == '\0') {
            return;
          }
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7ee0);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        if ((int)uVar9 < 0x23) {
          if (*(int *)(iVar10 + 0x828) == 0x21) {
            return;
          }
          iVar10 = *(int *)(param_1 + 0x150);
          if (iVar10 == 2) {
            cVar12 = _opd_FUN_00333a38(*(undefined4 *)(iVar3 + 0x24),0,0,0x91);
            if (cVar12 != '\0') {
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7ec4);
              *(undefined4 *)(param_1 + 0x44) = 0;
              *(undefined2 *)(*(int *)(iVar3 + 0x24) + 0x36e) = 0x8000;
              return;
            }
            iVar10 = *(int *)(param_1 + 0x150);
          }
          if (iVar10 != 3) {
            return;
          }
          cVar12 = _opd_FUN_00333608(*(undefined4 *)(iVar3 + 0x24),0,0);
          if (cVar12 == '\0') {
            return;
          }
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7ee4);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        goto LAB_00831d58;
      }
      if (uVar9 == 0x18) {
        cVar12 = _opd_FUN_00333608(*(undefined4 *)(iVar3 + 0x24),0,0);
        if (cVar12 == '\0') {
          return;
        }
        iVar11 = *(int *)(iVar11 + 0xff4);
        if (iVar11 < 0) {
          return;
        }
        if (1 < iVar11) {
          if (4 < iVar11) {
            return;
          }
          iVar10 = *(int *)(iVar10 + 0x828);
          if (iVar10 < 0x23) {
            if (0x20 < iVar10) goto LAB_008340a8;
            if (iVar10 != 0xe) {
              if (iVar10 < 0xf) {
                if (iVar10 != 10) {
                  if (iVar10 < 0xb) {
                    if (iVar10 != 2) {
                      if (iVar10 == 3) goto LAB_008340a8;
                      if (iVar10 != 1) goto LAB_00832994;
                      goto LAB_008347f8;
                    }
                  }
                  else if (iVar10 != 0xb) goto LAB_008340a8;
LAB_008347d4:
                  *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f5c);
                  *(undefined4 *)(param_1 + 0x44) = 0;
                  return;
                }
              }
              else if (iVar10 != 0x19) {
                if (iVar10 < 0x1a) {
                  if (iVar10 == 0x10) {
LAB_008340a8:
                    *(undefined4 *)(param_1 + 0x44) = 0;
                    *(undefined4 *)(param_1 + 0x40) = 0xcd;
                    return;
                  }
                  if (iVar10 < 0x10) goto LAB_008347d4;
                  if (iVar10 == 0x18) {
                    return;
                  }
                }
                else if ((iVar10 < 0x1c) || (iVar10 - 0x1dU < 2)) goto LAB_008340a8;
LAB_00832994:
                *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f64);
                *(undefined4 *)(param_1 + 0x44) = 0;
                return;
              }
            }
          }
          else if (iVar10 < 0x38) {
            if (0x34 < iVar10) goto LAB_008347d4;
            if (iVar10 != 0x2e) {
              if (iVar10 < 0x2f) {
                if (iVar10 - 0x25U < 3) goto LAB_008340a8;
              }
              else {
                if (iVar10 == 0x2f) goto LAB_008347d4;
                if (0x31 < iVar10) goto LAB_008347f8;
              }
              goto LAB_00832994;
            }
          }
          else if (iVar10 < 0x42) {
            if (iVar10 < 0x3e) goto LAB_008340a8;
          }
          else {
            if (iVar10 == 0x2cc) {
              return;
            }
            if (iVar10 != 0x2ce) {
              if (iVar10 != 0x43) goto LAB_00832994;
              goto LAB_008347d4;
            }
          }
LAB_008347f8:
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f64);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        iVar10 = *(int *)(iVar10 + 0x828);
        if (iVar10 < 0x1f) {
          if ((0x1c < iVar10) || (iVar10 == 0x10)) goto LAB_0083369c;
          if (iVar10 < 0x11) {
            if (iVar10 == 3) goto LAB_0083369c;
            if (iVar10 < 3) goto LAB_00833400;
            uVar9 = iVar10 - 0xc;
          }
          else {
            if (iVar10 == 0x18) {
              return;
            }
            if (iVar10 < 0x18) goto LAB_00833400;
            uVar9 = iVar10 - 0x1a;
          }
        }
        else {
          if (0x27 < iVar10) {
            if (0x37 < iVar10) {
              if (iVar10 < 0x3e) goto LAB_0083369c;
              if (iVar10 == 0x2cc) {
                return;
              }
            }
            goto LAB_00833400;
          }
          if (0x24 < iVar10) goto LAB_0083369c;
          uVar9 = iVar10 - 0x21;
        }
        if (1 < uVar9) {
LAB_00833400:
          *(undefined4 *)(param_1 + 0x44) = 0;
          *(undefined4 *)(param_1 + 0x40) = 0xc9;
          return;
        }
LAB_0083369c:
        *(undefined4 *)(param_1 + 0x44) = 0;
        *(undefined4 *)(param_1 + 0x40) = 0xcd;
        return;
      }
      if ((int)uVar9 < 0x19) {
        if (uVar9 != 0x13) {
          if (0x15 < (int)uVar9) {
            return;
          }
          cVar12 = _opd_FUN_00333608(*(undefined4 *)(iVar3 + 0x24),0,0);
          if (cVar12 == '\0') {
            return;
          }
          iVar10 = *(int *)(iVar10 + 0x828);
          if (iVar10 < 0x23) {
            if (0x20 < iVar10) goto LAB_00833610;
            if (iVar10 != 0xe) {
              if (0xe < iVar10) {
                if (iVar10 == 0x15) {
LAB_00833610:
                  *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f54);
                  *(undefined4 *)(param_1 + 0x44) = 0;
                  return;
                }
                if (iVar10 < 0x16) {
                  if (iVar10 == 0x10) goto LAB_00833610;
                  if (iVar10 < 0x10) goto LAB_00833fc8;
                  if (iVar10 == 0x14) goto LAB_00833c30;
                }
                else if ((iVar10 == 0x1b) || ((0x1a < iVar10 && (iVar10 - 0x1dU < 2))))
                goto LAB_00833610;
LAB_00831d18:
                *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f64);
                *(undefined4 *)(param_1 + 0x44) = 0;
                return;
              }
              if (iVar10 != 10) {
                if (iVar10 < 0xb) {
                  if (iVar10 != 2) {
                    if (iVar10 == 3) goto LAB_00833610;
                    if (iVar10 != 1) goto LAB_00831d18;
                    goto LAB_00833c30;
                  }
                }
                else if (iVar10 != 0xb) goto LAB_00833610;
LAB_00833fc8:
                *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f5c);
                *(undefined4 *)(param_1 + 0x44) = 0;
                return;
              }
            }
          }
          else if (iVar10 < 0x38) {
            if (0x34 < iVar10) goto LAB_00833fc8;
            if (iVar10 != 0x2e) {
              if (iVar10 < 0x2f) {
                if (iVar10 - 0x25U < 3) goto LAB_00833610;
              }
              else {
                if (iVar10 == 0x2f) goto LAB_00833fc8;
                if (0x31 < iVar10) goto LAB_00833c30;
              }
              goto LAB_00831d18;
            }
          }
          else if (iVar10 < 0x42) {
            if (iVar10 < 0x3e) goto LAB_00833610;
          }
          else {
            if (iVar10 == 0x43) goto LAB_00833fc8;
            if (iVar10 != 0x2ce) goto LAB_00831d18;
          }
LAB_00833c30:
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f64);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        cVar12 = _opd_FUN_00333608(*(undefined4 *)(iVar3 + 0x24),0,0);
        if (cVar12 == '\0') {
          return;
        }
        iVar10 = *(int *)(iVar10 + 0x828);
        if (iVar10 < 0x1f) {
          if (iVar10 < 0x1d) {
            if (iVar10 < 0xe) {
              if ((iVar10 < 0xc) && (iVar10 != 3)) {
                if (iVar10 < 4) {
                  if (iVar10 < 1) goto LAB_00833a9c;
                }
                else if (iVar10 < 10) goto LAB_00833a9c;
                goto LAB_00833194;
              }
            }
            else {
              if (iVar10 == 0x13) {
                return;
              }
              if (iVar10 < 0x14) {
                if (iVar10 < 0x10) goto LAB_00833194;
                if (iVar10 != 0x10) goto LAB_00833a9c;
              }
              else if (iVar10 != 0x15) {
                if (iVar10 < 0x15) goto LAB_00833194;
                if (iVar10 != 0x1b) goto LAB_00833a9c;
              }
            }
          }
LAB_00833ee4:
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f08);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        if (iVar10 < 0x38) {
          if (iVar10 < 0x35) {
            if (iVar10 < 0x28) {
              if ((0x24 < iVar10) || (iVar10 - 0x21U < 2)) goto LAB_00833ee4;
            }
            else if (iVar10 - 0x2eU < 2) goto LAB_00833194;
            goto LAB_00833a9c;
          }
        }
        else if (iVar10 < 0x42) {
          if (iVar10 < 0x3f) {
            if (iVar10 < 0x3e) goto LAB_00833ee4;
LAB_00833a9c:
            *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f04);
            *(undefined4 *)(param_1 + 0x44) = 0;
            return;
          }
        }
        else if ((iVar10 != 0x43) && (iVar10 != 0x2ce)) goto LAB_00833a9c;
LAB_00833194:
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f04);
        *(undefined4 *)(param_1 + 0x44) = 0;
        return;
      }
      if ((int)uVar9 < 0x1b) {
        cVar12 = _opd_FUN_00333608(*(undefined4 *)(iVar3 + 0x24),0,0);
        if (cVar12 == '\0') {
          return;
        }
        iVar10 = *(int *)(iVar10 + 0x828);
        if (iVar10 < 0x23) {
          if (0x20 < iVar10) goto LAB_008334a8;
          if (iVar10 != 0xe) {
            if (0xe < iVar10) {
              if (iVar10 < 0x1c) {
                if ((0x19 < iVar10) || (iVar10 == 0x10)) {
LAB_008334a8:
                  *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f54);
                  *(undefined4 *)(param_1 + 0x44) = 0;
                  return;
                }
                if (iVar10 < 0x10) goto LAB_00833d4c;
                if (iVar10 == 0x19) goto LAB_00833d84;
              }
              else if (iVar10 - 0x1dU < 2) goto LAB_008334a8;
LAB_00832808:
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f64);
              *(undefined4 *)(param_1 + 0x44) = 0;
              return;
            }
            if (iVar10 != 10) {
              if (iVar10 < 0xb) {
                if (iVar10 != 2) {
                  if (iVar10 == 3) goto LAB_008334a8;
                  if (iVar10 != 1) goto LAB_00832808;
                  goto LAB_00833d84;
                }
              }
              else if (iVar10 != 0xb) goto LAB_008334a8;
LAB_00833d4c:
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f5c);
              *(undefined4 *)(param_1 + 0x44) = 0;
              return;
            }
          }
        }
        else if (iVar10 < 0x38) {
          if (0x34 < iVar10) goto LAB_00833d4c;
          if (iVar10 != 0x2e) {
            if (iVar10 < 0x2f) {
              if (iVar10 - 0x25U < 3) goto LAB_008334a8;
            }
            else {
              if (iVar10 == 0x2f) goto LAB_00833d4c;
              if (0x31 < iVar10) goto LAB_00833d84;
            }
            goto LAB_00832808;
          }
        }
        else if (iVar10 < 0x42) {
          if (iVar10 < 0x3e) goto LAB_008334a8;
        }
        else {
          if (iVar10 == 0x43) goto LAB_00833d4c;
          if (iVar10 != 0x2ce) goto LAB_00832808;
        }
LAB_00833d84:
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f64);
        *(undefined4 *)(param_1 + 0x44) = 0;
        return;
      }
LAB_0083285c:
      if ((*(int *)(iVar10 + 0x8c) == 3) && (*(int *)(iVar10 + 0x828) - 0x3bU < 2)) {
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f4c);
        *(undefined4 *)(param_1 + 0x44) = 0;
        return;
      }
      uVar1 = *(undefined2 *)(iVar10 + 0x820);
      *(undefined2 *)(iVar11 + 0xe5e) = uVar1;
      *(undefined2 *)(iVar11 + 0xe5a) = uVar1;
      iVar10 = *(int *)(iVar10 + 0x828);
      if (iVar10 == 0x24) goto LAB_008328b4;
      if (iVar10 < 0x25) {
        if (iVar10 < 0xe) {
          if (0xb < iVar10) {
LAB_00833520:
            *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7ec4);
            *(ulonglong *)(param_1 + 0x140) = *(ulonglong *)(param_1 + 0x140) | 0x8000000000000000;
            *(undefined4 *)(param_1 + 0x44) = 0;
            return;
          }
          if (iVar10 < 5) {
            if (2 < iVar10) goto LAB_00833520;
            if (iVar10 != 1) {
              if (iVar10 != 2) {
                return;
              }
              goto LAB_008328b4;
            }
          }
          else if (iVar10 != 10) {
            if (iVar10 != 0xb) {
              return;
            }
            goto LAB_008328b4;
          }
        }
        else {
          if (iVar10 == 0x1c) {
            if (bVar7) {
              return;
            }
            uVar6 = *(undefined4 *)(puVar8 + -0x7f7c);
            *(undefined4 *)(param_1 + 0x44) = 0;
            *(undefined4 *)(param_1 + 0x40) = uVar6;
            return;
          }
          if (iVar10 < 0x1d) {
            if (iVar10 == 0xf) goto LAB_008328b4;
            if (0xe < iVar10) {
              if (iVar10 != 0x10) {
                return;
              }
              goto LAB_00833520;
            }
          }
          else {
            if (iVar10 == 0x20) {
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7ec0);
              *(ulonglong *)(param_1 + 0x140) = *(ulonglong *)(param_1 + 0x140) | 0x8000000000000000
              ;
              *(undefined4 *)(param_1 + 0x44) = 0;
              return;
            }
            if (iVar10 != 0x23) {
              if (iVar10 != 0x1f) {
                return;
              }
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7ec4);
              *(ulonglong *)(param_1 + 0x140) = *(ulonglong *)(param_1 + 0x140) | 0x8000000000000000
              ;
              *(undefined4 *)(param_1 + 0x44) = 0;
              return;
            }
          }
        }
      }
      else if (iVar10 < 0x38) {
        if (0x34 < iVar10) {
LAB_008328b4:
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7ec8);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        if (iVar10 != 0x2e) {
          if (iVar10 < 0x2f) {
            if (iVar10 == 0x26) {
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7eb8);
              *(undefined4 *)(param_1 + 0x44) = 0;
              return;
            }
            if (0x25 < iVar10) {
              if (iVar10 != 0x27) {
                return;
              }
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7eb4);
              *(undefined4 *)(param_1 + 0x44) = 0;
              return;
            }
            *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7ebc);
            *(undefined4 *)(param_1 + 0x44) = 0;
            return;
          }
          if (iVar10 == 0x2f) goto LAB_008328b4;
          if (iVar10 < 0x32) {
            return;
          }
        }
      }
      else if (iVar10 < 0x42) {
        if (iVar10 < 0x3e) {
          if (0x3a < iVar10) {
            return;
          }
          goto LAB_00833520;
        }
      }
      else {
        if (iVar10 == 0x43) goto LAB_008328b4;
        if (iVar10 != 0x2ce) {
          return;
        }
      }
      *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7ecc);
      *(undefined4 *)(param_1 + 0x44) = 0;
      return;
    }
    if (0x10 < (int)uVar9) {
      uVar4 = *(uint *)(iVar10 + 0x828);
      if (uVar9 == uVar4) {
        return;
      }
      if (0x43 < uVar4) {
        cVar12 = _opd_FUN_00333608(*(undefined4 *)(iVar3 + 0x24),0,0);
        if (cVar12 == '\0') {
          return;
        }
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f64);
        *(undefined4 *)(param_1 + 0x44) = 0;
        return;
      }
                    /* WARNING: Could not recover jumptable at 0x008321bc. Too many branches */
                    /* WARNING: Treating indirect jump as call */
      (*(code *)(*(int *)(uVar4 * 4 + *(int *)(PTR_DAT_0121b88c + -0x7f10)) +
                *(int *)(PTR_DAT_0121b88c + -0x7f10)))();
      return;
    }
    if (uVar9 == 9) {
      cVar12 = _opd_FUN_00333a38(*(undefined4 *)(iVar3 + 0x24),0,0,0x96);
      if ((cVar12 != '\0') && (*(int *)(iVar10 + 0x828) == 1)) {
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f64);
        *(undefined4 *)(param_1 + 0x44) = 0;
      }
      cVar12 = _opd_FUN_00333608(*(undefined4 *)(iVar3 + 0x24),0,0);
      if (cVar12 == '\0') {
        return;
      }
      if (*(int *)(iVar10 + 0x828) != 0xe) {
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f30);
        *(undefined4 *)(param_1 + 0x44) = 0;
        return;
      }
      *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f2c);
      *(undefined4 *)(param_1 + 0x44) = 0;
      return;
    }
    if ((int)uVar9 < 10) {
      if (uVar9 == 4) {
        iVar10 = *(int *)(iVar10 + 0x828);
        if (iVar10 == 0x1b) {
LAB_00833b90:
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ee4);
          *(ulonglong *)(param_1 + 0x140) = *(ulonglong *)(param_1 + 0x140) | 0x8000000000000000;
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        if (iVar10 < 0x1c) {
          if (iVar10 != 0xb) {
            if (iVar10 < 0xc) {
              if (iVar10 != 6) {
                if (iVar10 < 7) {
                  if (iVar10 == 2) goto LAB_00833be4;
                  if (iVar10 == 3) {
                    *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f54);
                    *(undefined4 *)(param_1 + 0x44) = 0;
                    return;
                  }
                  if (iVar10 != 1) {
                    return;
                  }
                }
                else {
                  if (iVar10 == 8) goto LAB_00834280;
                  if (8 < iVar10) goto LAB_00834268;
                }
                goto LAB_00832f64;
              }
              goto LAB_00833be4;
            }
            if (iVar10 != 0xf) {
              if (iVar10 < 0x10) {
                if (iVar10 == 0xd) {
                  *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ed4);
                  *(undefined4 *)(param_1 + 0x44) = 0;
                  return;
                }
                if (iVar10 < 0xe) {
                  *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ed8);
                  *(undefined4 *)(param_1 + 0x44) = 0;
                  return;
                }
LAB_00834268:
                *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ef0);
                *(undefined4 *)(param_1 + 0x44) = 0;
                return;
              }
              if (iVar10 < 0x15) {
                if (iVar10 < 0x13) {
                  if (iVar10 != 0x10) {
                    return;
                  }
                  *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ed0);
                  *(undefined4 *)(param_1 + 0x44) = 0;
                  return;
                }
              }
              else if (iVar10 != 0x18) {
                return;
              }
              goto LAB_00832f64;
            }
          }
LAB_00834280:
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ee8);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        if (iVar10 < 0x38) {
          if (iVar10 < 0x35) {
            if (iVar10 < 0x28) {
              if (iVar10 < 0x25) {
                if (iVar10 == 0x1d) {
                  *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ee4);
                  *(ulonglong *)(param_1 + 0x140) =
                       *(ulonglong *)(param_1 + 0x140) | 0x8000000000000000;
                  *(undefined4 *)(param_1 + 0x44) = 0;
                  return;
                }
                if (iVar10 != 0x1e) {
                  return;
                }
                *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ee0);
                *(ulonglong *)(param_1 + 0x140) =
                     *(ulonglong *)(param_1 + 0x140) | 0x8000000000000000;
                *(undefined4 *)(param_1 + 0x44) = 0;
                return;
              }
              goto LAB_00833b90;
            }
            if (iVar10 != 0x2f) {
              if (iVar10 < 0x30) {
                if (iVar10 != 0x2e) {
                  return;
                }
              }
              else if (iVar10 < 0x32) {
                return;
              }
              goto LAB_00832f64;
            }
          }
LAB_00833be4:
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7eec);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        if (iVar10 < 0x42) {
          if (iVar10 < 0x3e) {
            if (iVar10 < 0x3b) {
              return;
            }
            goto LAB_00833b90;
          }
        }
        else if ((iVar10 != 0x2cc) && (iVar10 != 0x2ce)) {
          if (iVar10 != 0x43) {
            return;
          }
          goto LAB_00833be4;
        }
LAB_00832f64:
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ef4);
        *(undefined4 *)(param_1 + 0x44) = 0;
        return;
      }
      if (4 < (int)uVar9) {
        if (7 < (int)uVar9) {
          cVar12 = _opd_FUN_00333a38(*(undefined4 *)(iVar3 + 0x24),0,0,0x96);
          if ((cVar12 != '\0') &&
             (((iVar11 = *(int *)(iVar10 + 0x828), iVar11 == 0x2f || (iVar11 == 0x43)) ||
              (iVar11 == 2)))) {
            *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f5c);
            *(undefined4 *)(param_1 + 0x44) = 0;
          }
          cVar12 = _opd_FUN_00333608(*(undefined4 *)(iVar3 + 0x24),0,0);
          if (cVar12 == '\0') {
            return;
          }
          if (*(int *)(iVar10 + 0x828) != 0xf) {
            *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f6c);
            *(undefined4 *)(param_1 + 0x44) = 0;
            return;
          }
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f0c);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        goto LAB_00831d58;
      }
      if (uVar9 == 2) goto LAB_00832a90;
      if ((int)uVar9 < 3) {
        if (uVar9 != 1) {
          return;
        }
        goto LAB_00831e40;
      }
      goto LAB_00832050;
    }
    if ((int)uVar9 < 0xe) {
      if ((int)uVar9 < 0xc) {
        if (uVar9 == 10) {
          if ((*(short *)(param_1 + 0xce) == 0) || (*(longlong *)(iVar11 + 0x1318) < 0)) {
            if (*(uint *)(param_1 + 0xdc) < 0x1f) {
              iVar11 = _opd_FUN_00333360(*(undefined4 *)(iVar3 + 0x24),0,0);
              if (iVar11 - 0x23U < 100) {
                *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f20);
                *(undefined4 *)(param_1 + 0x44) = 0;
              }
              else {
                *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f1c);
                *(undefined4 *)(param_1 + 0x44) = 0;
              }
            }
            else {
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f64);
              *(undefined4 *)(param_1 + 0x44) = 0;
            }
          }
          else {
            cVar12 = _opd_FUN_003feed0(param_1);
            if (cVar12 != '\0') {
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f18);
              *(undefined4 *)(param_1 + 0x44) = 0;
              return;
            }
            if (*(uint *)(iVar10 + 0x828) < 0x44) {
                    /* WARNING: Could not recover jumptable at 0x00834588. Too many branches */
                    /* WARNING: Treating indirect jump as call */
              (*(code *)(*(int *)(*(uint *)(iVar10 + 0x828) * 4 + *(int *)(puVar8 + -0x7f14)) +
                        *(int *)(puVar8 + -0x7f14)))();
              return;
            }
          }
          iVar11 = *(int *)(iVar10 + 0x8c);
          if (iVar11 != 6) {
            if (iVar11 != 9) {
              if (iVar11 != 4) {
                return;
              }
              if (*(int *)(iVar10 + 0x824) != 0x2c8) {
                return;
              }
              *(undefined4 *)(param_1 + 0x44) = 0;
              *(undefined4 *)(param_1 + 0x40) = 0xc5;
              return;
            }
            if ((*(int *)(iVar10 + 0x824) != 0x2cc) && (*(int *)(iVar10 + 0x824) != 0x2dd)) {
              return;
            }
            *(undefined4 *)(param_1 + 0x44) = 0;
            *(undefined4 *)(param_1 + 0x40) = 0xc5;
          }
          if (*(int *)(iVar10 + 0x824) != 0x2dd) {
            return;
          }
          *(undefined4 *)(param_1 + 0x44) = 0;
          *(undefined4 *)(param_1 + 0x40) = 0xc5;
          return;
        }
        if (uVar9 != 0xb) {
          return;
        }
        if ((*(int *)(iVar10 + 0x8c) == 0) && (*(int *)(iVar10 + 0x828) == 0x42)) {
          uVar6 = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f68);
          *(undefined4 *)(param_1 + 0x44) = 0;
          *(undefined4 *)(param_1 + 0x40) = uVar6;
        }
        if ((*(short *)(param_1 + 0xce) == 0) || (*(longlong *)(iVar11 + 0x1318) < 0)) {
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f5c);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        cVar12 = _opd_FUN_003feed0(param_1);
        if (cVar12 != '\0') {
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7ef8);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        iVar10 = *(int *)(iVar10 + 0x828);
        if (iVar10 != 0x18) {
          if (0x18 < iVar10) {
            if (iVar10 < 0x3b) {
              if (0x37 < iVar10) goto LAB_00834948;
              if (iVar10 < 0x35) {
                if ((0x31 < iVar10) || (iVar10 == 0x2e)) goto LAB_008349d8;
                if (iVar10 != 0x2f) {
                  return;
                }
              }
            }
            else if (iVar10 != 0x43) {
              if (iVar10 < 0x44) {
                if (3 < iVar10 - 0x3eU) {
                  return;
                }
              }
              else if ((iVar10 != 0x2cc) && (iVar10 != 0x2ce)) {
                return;
              }
              goto LAB_008349d8;
            }
LAB_00834884:
            *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f5c);
            *(undefined4 *)(param_1 + 0x44) = 0;
            return;
          }
          if (iVar10 < 0xe) {
            if ((iVar10 < 0xc) && (iVar10 != 3)) {
              if (iVar10 < 4) {
                if (iVar10 != 1) {
                  if (iVar10 != 2) {
                    return;
                  }
                  goto LAB_00834884;
                }
                goto LAB_008349d8;
              }
              if (iVar10 != 5) {
                if (iVar10 != 10) {
                  return;
                }
                *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f30);
                *(undefined4 *)(param_1 + 0x44) = 0;
                return;
              }
            }
          }
          else if (iVar10 != 0x10) {
            if (iVar10 < 0x11) {
              if (iVar10 != 0xe) {
                if (iVar10 != 0xf) {
                  return;
                }
                *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f0c);
                *(undefined4 *)(param_1 + 0x44) = 0;
                return;
              }
            }
            else if (2 < iVar10 - 0x13U) {
              return;
            }
            goto LAB_008349d8;
          }
LAB_00834948:
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f00);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
LAB_008349d8:
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f64);
        *(undefined4 *)(param_1 + 0x44) = 0;
        return;
      }
      uVar4 = *(uint *)(iVar10 + 0x828);
      if (uVar9 == uVar4) {
        return;
      }
      if (uVar4 != 0x18) {
        if ((int)uVar4 < 0x19) {
          if ((int)uVar4 < 0xb) {
            if ((int)uVar4 < 9) {
              if (uVar4 == 4) {
                if (bVar7) {
                  return;
                }
                uVar6 = *(undefined4 *)(PTR_DAT_0121b88c + -0x7edc);
                *(undefined4 *)(param_1 + 0x44) = 0;
                *(undefined4 *)(param_1 + 0x40) = uVar6;
                return;
              }
              if ((int)uVar4 < 5) {
                if (uVar4 == 2) goto LAB_0083443c;
                if (2 < (int)uVar4) {
                  *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f54);
                  *(undefined4 *)(param_1 + 0x44) = 0;
                  return;
                }
                if (uVar4 != 1) {
                  return;
                }
              }
              else if (uVar4 != 7) {
                if (7 < (int)uVar4) goto LAB_00834bf8;
                if (uVar4 != 6) {
                  return;
                }
                goto LAB_0083443c;
              }
              goto LAB_00833054;
            }
          }
          else if (uVar4 != 0xe) {
            if ((int)uVar4 < 0xf) {
              if (uVar4 == 0xc) {
                *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ed8);
                *(undefined4 *)(param_1 + 0x44) = 0;
                return;
              }
              if (0xc < (int)uVar4) {
                *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ed4);
                *(undefined4 *)(param_1 + 0x44) = 0;
                return;
              }
            }
            else {
              if (uVar4 == 0x10) {
                *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ed0);
                *(undefined4 *)(param_1 + 0x44) = 0;
                return;
              }
              if (0xf < (int)uVar4) {
                if (2 < uVar4 - 0x13) {
                  return;
                }
                goto LAB_00833054;
              }
            }
LAB_00834bf8:
            *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ee8);
            *(undefined4 *)(param_1 + 0x44) = 0;
            return;
          }
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ef0);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        if ((int)uVar4 < 0x35) {
          if ((int)uVar4 < 0x32) {
            if ((int)uVar4 < 0x28) {
              if ((int)uVar4 < 0x25) {
                if (uVar4 == 0x1d) {
                  *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ee4);
                  *(ulonglong *)(param_1 + 0x140) =
                       *(ulonglong *)(param_1 + 0x140) | 0x8000000000000000;
                  *(undefined4 *)(param_1 + 0x44) = 0;
                  return;
                }
                if (uVar4 == 0x1e) {
                  *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ee0);
                  *(ulonglong *)(param_1 + 0x140) =
                       *(ulonglong *)(param_1 + 0x140) | 0x8000000000000000;
                  *(undefined4 *)(param_1 + 0x44) = 0;
                  return;
                }
                if (uVar4 != 0x1b) {
                  return;
                }
              }
LAB_0083365c:
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ee4);
              *(ulonglong *)(param_1 + 0x140) = *(ulonglong *)(param_1 + 0x140) | 0x8000000000000000
              ;
              *(undefined4 *)(param_1 + 0x44) = 0;
              return;
            }
            if (uVar4 != 0x2e) {
              if (uVar4 != 0x2f) {
                return;
              }
              goto LAB_0083443c;
            }
          }
        }
        else if ((int)uVar4 < 0x42) {
          if ((int)uVar4 < 0x3e) {
            if (0x37 < (int)uVar4) {
              if ((int)uVar4 < 0x3b) {
                return;
              }
              goto LAB_0083365c;
            }
LAB_0083443c:
            *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7eec);
            *(undefined4 *)(param_1 + 0x44) = 0;
            return;
          }
        }
        else if ((uVar4 != 0x2cc) && (uVar4 != 0x2ce)) {
          if (uVar4 != 0x43) {
            return;
          }
          goto LAB_0083443c;
        }
      }
LAB_00833054:
      *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ef4);
      *(undefined4 *)(param_1 + 0x44) = 0;
      return;
    }
    if (uVar9 == 0xf) {
      iVar10 = *(int *)(iVar10 + 0x828);
      if (iVar10 == 0x2e) goto LAB_00833788;
      if (0x2e < iVar10) {
        if (iVar10 < 0x3b) {
          if (0x37 < iVar10) goto LAB_00833cb0;
          if (iVar10 < 0x35) {
            if (0x31 < iVar10) goto LAB_00833788;
            if (iVar10 != 0x2f) {
              return;
            }
          }
        }
        else if (iVar10 != 0x43) {
          if (iVar10 < 0x44) {
            if (3 < iVar10 - 0x3eU) {
              return;
            }
          }
          else if ((iVar10 != 0x2cc) && (iVar10 != 0x2ce)) {
            return;
          }
          goto LAB_00833788;
        }
LAB_00833468:
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f5c);
        *(undefined4 *)(param_1 + 0x44) = 0;
        return;
      }
      if (iVar10 < 0xe) {
        if (iVar10 < 0xc) {
          if (5 < iVar10) {
            if (iVar10 != 0xb) {
              return;
            }
            *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f6c);
            *(undefined4 *)(param_1 + 0x44) = 0;
            return;
          }
          if (iVar10 < 3) {
            if (iVar10 != 1) {
              if (iVar10 != 2) {
                return;
              }
              goto LAB_00833468;
            }
            goto LAB_00833788;
          }
        }
LAB_00833cb0:
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f00);
        *(undefined4 *)(param_1 + 0x44) = 0;
        return;
      }
      if (iVar10 < 0x16) {
        if (iVar10 < 0x13) {
          if (iVar10 == 0xe) {
            *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f2c);
            *(undefined4 *)(param_1 + 0x44) = 0;
            return;
          }
          if (iVar10 != 0x10) {
            return;
          }
          goto LAB_00833cb0;
        }
      }
      else if (iVar10 != 0x18) {
        return;
      }
LAB_00833788:
      *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f64);
      *(undefined4 *)(param_1 + 0x44) = 0;
      return;
    }
    if (0xf < (int)uVar9) {
      iVar10 = *(int *)(iVar10 + 0x828);
      if (iVar10 == 0x1d) {
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ee4);
        *(ulonglong *)(param_1 + 0x140) = *(ulonglong *)(param_1 + 0x140) | 0x8000000000000000;
        *(undefined4 *)(param_1 + 0x44) = 0;
        return;
      }
      if (0x1d < iVar10) {
        if (iVar10 < 0x38) {
          if (0x34 < iVar10) goto LAB_00833dc0;
          if (iVar10 != 0x2e) {
            if (iVar10 < 0x2f) {
              if (iVar10 == 0x1e) {
                *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ee0);
                *(ulonglong *)(param_1 + 0x140) =
                     *(ulonglong *)(param_1 + 0x140) | 0x8000000000000000;
                *(undefined4 *)(param_1 + 0x44) = 0;
                return;
              }
              if (2 < iVar10 - 0x25U) {
                return;
              }
              goto LAB_00833970;
            }
            if (iVar10 == 0x2f) goto LAB_00833dc0;
            if (iVar10 < 0x32) {
              return;
            }
          }
        }
        else if (iVar10 < 0x42) {
          if (iVar10 < 0x3e) {
            if (iVar10 < 0x3b) {
LAB_008341f8:
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f54);
              *(undefined4 *)(param_1 + 0x44) = 0;
              return;
            }
LAB_00833970:
            *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ee4);
            *(ulonglong *)(param_1 + 0x140) = *(ulonglong *)(param_1 + 0x140) | 0x8000000000000000;
            *(undefined4 *)(param_1 + 0x44) = 0;
            return;
          }
        }
        else if ((iVar10 != 0x2cc) && (iVar10 != 0x2ce)) {
          if (iVar10 != 0x43) {
            return;
          }
          goto LAB_00833dc0;
        }
        goto LAB_00832a78;
      }
      if (10 < iVar10) {
        if (iVar10 != 0xf) {
          if (0xf < iVar10) {
            if (iVar10 != 0x18) {
              if (0x18 < iVar10) {
                if (iVar10 != 0x1b) {
                  return;
                }
                goto LAB_00833970;
              }
              if (2 < iVar10 - 0x13U) {
                return;
              }
            }
            goto LAB_00832a78;
          }
          if (iVar10 == 0xc) {
            *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ed8);
            *(undefined4 *)(param_1 + 0x44) = 0;
            return;
          }
          if (0xb < iVar10) {
            if (iVar10 == 0xd) {
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ed4);
              *(undefined4 *)(param_1 + 0x44) = 0;
              return;
            }
            if (iVar10 != 0xe) {
              return;
            }
            goto LAB_00834238;
          }
        }
LAB_008343e8:
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ee8);
        *(undefined4 *)(param_1 + 0x44) = 0;
        return;
      }
      if (8 < iVar10) {
LAB_00834238:
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ef0);
        *(undefined4 *)(param_1 + 0x44) = 0;
        return;
      }
      if (iVar10 == 4) {
        if (bVar7) {
          return;
        }
        uVar6 = *(undefined4 *)(PTR_DAT_0121b88c + -0x7edc);
        *(undefined4 *)(param_1 + 0x44) = 0;
        *(undefined4 *)(param_1 + 0x40) = uVar6;
        return;
      }
      if (iVar10 < 5) {
        if (iVar10 == 2) {
LAB_00833dc0:
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7eec);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        if (2 < iVar10) goto LAB_008341f8;
        if (iVar10 != 1) {
          return;
        }
      }
      else if (iVar10 != 7) {
        if (7 < iVar10) goto LAB_008343e8;
        if (iVar10 != 6) {
          return;
        }
        goto LAB_00833dc0;
      }
LAB_00832a78:
      *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ef4);
      *(undefined4 *)(param_1 + 0x44) = 0;
      return;
    }
    iVar11 = *(int *)(iVar10 + 0x828);
    if (iVar11 < 0x16) {
      if (0x12 < iVar11) {
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f90);
        *(undefined4 *)(param_1 + 0x44) = 0;
        goto LAB_0083333c;
      }
      if (iVar11 == 10) {
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f30);
        *(undefined4 *)(param_1 + 0x44) = 0;
        goto LAB_0083333c;
      }
      if (iVar11 < 0xb) {
        if (iVar11 == 1) {
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f64);
          *(undefined4 *)(param_1 + 0x44) = 0;
          goto LAB_0083333c;
        }
        if (iVar11 != 2) goto LAB_0083333c;
      }
      else if (iVar11 != 0xb) {
        if (iVar11 == 0xf) {
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f0c);
          *(undefined4 *)(param_1 + 0x44) = 0;
        }
        goto LAB_0083333c;
      }
LAB_00834d28:
      *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f5c);
      *(undefined4 *)(param_1 + 0x44) = 0;
    }
    else {
      if (iVar11 == 0x2e) {
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f34);
        *(undefined4 *)(param_1 + 0x44) = 0;
        goto LAB_0083333c;
      }
      if (iVar11 < 0x2f) {
        if (2 < iVar11 - 0x18U) goto LAB_0083333c;
      }
      else {
        if (iVar11 == 0x43) goto LAB_00834d28;
        if (iVar11 != 0x2cc) goto LAB_0083333c;
      }
      *(undefined4 *)(param_1 + 0x44) = 0;
      *(undefined4 *)(param_1 + 0x40) = 0xc5;
    }
LAB_0083333c:
    iVar11 = *(int *)(iVar10 + 0x8c);
    if (iVar11 != 6) {
      if (iVar11 != 9) {
        if (iVar11 != 4) {
          return;
        }
        if (*(int *)(iVar10 + 0x824) != 0x2c8) {
          return;
        }
        *(undefined4 *)(param_1 + 0x44) = 0;
        *(undefined4 *)(param_1 + 0x40) = 0xc5;
        return;
      }
      if ((*(int *)(iVar10 + 0x824) != 0x2cc) && (*(int *)(iVar10 + 0x824) != 0x2dd)) {
        return;
      }
      *(undefined4 *)(param_1 + 0x44) = 0;
      *(undefined4 *)(param_1 + 0x40) = 0xc5;
    }
    if (*(int *)(iVar10 + 0x824) != 0x2dd) {
      return;
    }
    *(undefined4 *)(param_1 + 0x44) = 0;
    *(undefined4 *)(param_1 + 0x40) = 0xc5;
    return;
  }
  if (uVar9 == 0x38) {
    if (*(int *)(iVar10 + 0x82c) != 0) {
      *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f54);
      *(undefined4 *)(param_1 + 0x44) = 0;
      return;
    }
    if (*(int *)(iVar10 + 0x828) - 0x38U < 2) {
      cVar12 = _opd_FUN_00333608(*(undefined4 *)(iVar3 + 0x24),0,0);
      if (cVar12 == '\0') {
        return;
      }
      uVar6 = *(undefined4 *)(puVar8 + -0x7e98);
      *(undefined4 *)(param_1 + 0x44) = 0;
      *(undefined4 *)(param_1 + 0x40) = uVar6;
      return;
    }
    uVar6 = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f54);
    *(undefined4 *)(param_1 + 0x44) = 0;
    *(undefined4 *)(param_1 + 0x40) = uVar6;
    return;
  }
  if (0x38 < (int)uVar9) {
    if (uVar9 == 0x40) {
      if (*(int *)(iVar10 + 0x828) - 0x3fU < 2) {
        return;
      }
      if (*(int *)(iVar10 + 0x8c) == 0) {
        uVar6 = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f24);
        *(undefined4 *)(param_1 + 0x44) = 0;
        *(undefined4 *)(param_1 + 0x40) = uVar6;
        return;
      }
      *(undefined4 *)(param_1 + 0x44) = 0;
      *(undefined4 *)(param_1 + 0x40) = 0xd9;
      return;
    }
    if ((int)uVar9 < 0x41) {
      if (uVar9 == 0x3c) {
        if (*(int *)(iVar10 + 0x82c) != 0) {
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f80);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        iVar10 = *(int *)(iVar10 + 0x828);
        if (0x3a < iVar10) {
          if (iVar10 < 0x3d) {
            return;
          }
          if (iVar10 == 0x3d) {
            uVar6 = *(undefined4 *)(PTR_DAT_0121b88c + -0x7e8c);
            *(undefined4 *)(param_1 + 0x44) = 0;
            *(undefined4 *)(param_1 + 0x40) = uVar6;
            return;
          }
        }
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f80);
        *(undefined4 *)(param_1 + 0x44) = 0;
        return;
      }
      if ((int)uVar9 < 0x3d) {
        if (uVar9 == 0x3a) {
          if (*(int *)(iVar10 + 0x82c) != 0) {
            *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f54);
            *(undefined4 *)(param_1 + 0x44) = 0;
            return;
          }
          if ((*(int *)(iVar10 + 0x828) != 3) && (*(int *)(iVar10 + 0x828) != 0x3a)) {
            uVar6 = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f54);
            *(undefined4 *)(param_1 + 0x44) = 0;
            *(undefined4 *)(param_1 + 0x40) = uVar6;
            return;
          }
          cVar12 = _opd_FUN_00333608(*(undefined4 *)(iVar3 + 0x24),0,0);
          if (cVar12 == '\0') {
            return;
          }
          uVar6 = *(undefined4 *)(puVar8 + -0x7f54);
          *(undefined4 *)(param_1 + 0x44) = 0;
          *(undefined4 *)(param_1 + 0x40) = uVar6;
          return;
        }
        if (0x3a < (int)uVar9) {
          if (*(int *)(iVar10 + 0x82c) != 0) {
            *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f80);
            *(undefined4 *)(param_1 + 0x44) = 0;
            return;
          }
          if (*(int *)(iVar10 + 0x828) - 0x3bU < 2) {
            cVar12 = _opd_FUN_00333608(*(undefined4 *)(iVar3 + 0x24),0,0);
            if (cVar12 == '\0') {
              return;
            }
            uVar6 = *(undefined4 *)(puVar8 + -0x7e90);
            *(undefined4 *)(param_1 + 0x44) = 0;
            *(undefined4 *)(param_1 + 0x40) = uVar6;
            return;
          }
          uVar6 = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f80);
          *(undefined4 *)(param_1 + 0x44) = 0;
          *(undefined4 *)(param_1 + 0x40) = uVar6;
          return;
        }
        if (*(int *)(iVar10 + 0x82c) != 0) {
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f54);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        iVar10 = *(int *)(iVar10 + 0x828);
        if (0x37 < iVar10) {
          if (iVar10 < 0x3a) {
            return;
          }
          if (iVar10 == 0x3a) {
            uVar6 = *(undefined4 *)(PTR_DAT_0121b88c + -0x7e94);
            *(undefined4 *)(param_1 + 0x44) = 0;
            *(undefined4 *)(param_1 + 0x40) = uVar6;
            return;
          }
        }
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f54);
        *(undefined4 *)(param_1 + 0x44) = 0;
        return;
      }
      if (uVar9 == 0x3e) {
        if (*(int *)(iVar10 + 0x82c) != 0) {
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f64);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        if (*(int *)(iVar10 + 0x828) == 0x3e) {
          return;
        }
        uVar6 = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f64);
        *(undefined4 *)(param_1 + 0x44) = 0;
        *(undefined4 *)(param_1 + 0x40) = uVar6;
        return;
      }
      if ((int)uVar9 < 0x3f) {
        if (*(int *)(iVar10 + 0x82c) != 0) {
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f80);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        if ((*(int *)(iVar10 + 0x828) != 0x1b) && (*(int *)(iVar10 + 0x828) != 0x3d)) {
          uVar6 = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f80);
          *(undefined4 *)(param_1 + 0x44) = 0;
          *(undefined4 *)(param_1 + 0x40) = uVar6;
          return;
        }
        cVar12 = _opd_FUN_00333608(*(undefined4 *)(iVar3 + 0x24),0,0);
        if (cVar12 == '\0') {
          return;
        }
        uVar6 = *(undefined4 *)(puVar8 + -0x7f80);
        *(undefined4 *)(param_1 + 0x44) = 0;
        *(undefined4 *)(param_1 + 0x40) = uVar6;
        return;
      }
    }
    else {
      if (uVar9 == 0x44) {
        if (*(int *)(iVar10 + 0x828) == 0x44) {
          return;
        }
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f64);
        *(undefined4 *)(param_1 + 0x44) = 0;
        return;
      }
      if (0x44 < (int)uVar9) {
        if (uVar9 == 0x2cc) {
          cVar12 = _opd_FUN_00333608(*(undefined4 *)(iVar3 + 0x24),0,0);
          if (cVar12 == '\0') {
            return;
          }
          iVar10 = *(int *)(param_1 + 0x24c);
          if (iVar10 < 0) {
            return;
          }
          if (iVar10 < 2) {
            *(undefined4 *)(param_1 + 0x44) = 0;
            *(undefined4 *)(param_1 + 0x40) = 0xc9;
            return;
          }
          if (4 < iVar10) {
            return;
          }
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f64);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        if ((int)uVar9 < 0x2cd) {
          if (uVar9 != 0x2c8) {
            return;
          }
          if (*(int *)(iVar10 + 0x824) != 1) {
            if (*(int *)(iVar10 + 0x824) == 0x2c8) {
              return;
            }
            *(undefined4 *)(param_1 + 0x40) = 0;
            *(undefined4 *)(param_1 + 0x44) = 0;
            return;
          }
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f64);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        if (uVar9 == 0x2ce) {
          if (*(int *)(iVar10 + 0x828) == 0x41) {
            return;
          }
          if (*(int *)(iVar10 + 0x828) == 0x2ce) {
            return;
          }
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f64);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        if (uVar9 != 0x2dd) {
          return;
        }
        puVar5 = *(undefined4 **)(param_1 + 0x38);
        if (puVar5 != (undefined4 *)0x0) {
          if (((uint)puVar5 & 1) == 0) {
            iVar11 = *(int *)(param_1 + 0x3c);
          }
          else {
            iVar11 = *(int *)(param_1 + 0x3c);
            puVar5 = *(undefined4 **)((int)puVar5 + *(int *)(param_1 + iVar11) + -1);
          }
          (*(code *)*puVar5)(param_1 + iVar11,0);
        }
        if (*(int *)(iVar10 + 0x824) != 1) {
          return;
        }
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f64);
        *(undefined4 *)(param_1 + 0x44) = 0;
        return;
      }
      if (uVar9 == 0x42) {
        if (*(int *)(iVar10 + 0x824) == 0x42) {
          return;
        }
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f5c);
        *(undefined4 *)(param_1 + 0x44) = 0;
        return;
      }
      if (0x42 < (int)uVar9) {
        if (*(int *)(iVar10 + 0x82c) != 0) {
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f5c);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        if (*(int *)(iVar10 + 0x828) == 0x43) {
          return;
        }
        uVar6 = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f5c);
        *(undefined4 *)(param_1 + 0x44) = 0;
        *(undefined4 *)(param_1 + 0x40) = uVar6;
        return;
      }
    }
LAB_00831d58:
    puVar5 = *(undefined4 **)(param_1 + 0x38);
    if (puVar5 == (undefined4 *)0x0) {
      return;
    }
    if (((uint)puVar5 & 1) == 0) {
      iVar10 = *(int *)(param_1 + 0x3c);
    }
    else {
      iVar10 = *(int *)(param_1 + 0x3c);
      puVar5 = *(undefined4 **)((int)puVar5 + *(int *)(param_1 + iVar10) + -1);
    }
    (*(code *)*puVar5)(param_1 + iVar10,0);
    return;
  }
  if (uVar9 == 0x30) {
LAB_00832050:
    if ((*(int *)(iVar10 + 0x8c) == 3) && (*(int *)(iVar10 + 0x828) - 0x38U < 2)) {
      *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f50);
      *(undefined4 *)(param_1 + 0x44) = 0;
      return;
    }
    uVar1 = *(undefined2 *)(iVar10 + 0x820);
    *(undefined2 *)(iVar11 + 0xe5e) = uVar1;
    *(undefined2 *)(iVar11 + 0xe5a) = uVar1;
    iVar10 = *(int *)(iVar10 + 0x828);
    if (iVar10 == 0x1b) {
LAB_00832834:
      *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7ee4);
      *(ulonglong *)(param_1 + 0x140) = *(ulonglong *)(param_1 + 0x140) | 0x8000000000000000;
      *(undefined4 *)(param_1 + 0x44) = 0;
      return;
    }
    if (iVar10 < 0x1c) {
      if (iVar10 != 0xb) {
        if (iVar10 < 0xc) {
          if (iVar10 != 6) {
            if (iVar10 < 7) {
              if (iVar10 == 2) goto LAB_00832d24;
              if (iVar10 == 4) {
                if (bVar7) {
                  return;
                }
                uVar6 = *(undefined4 *)(puVar8 + -0x7edc);
                *(undefined4 *)(param_1 + 0x44) = 0;
                *(undefined4 *)(param_1 + 0x40) = uVar6;
                return;
              }
              if (iVar10 != 1) {
                return;
              }
            }
            else {
              if (iVar10 == 8) goto LAB_00832d38;
              if (8 < iVar10) goto LAB_00833858;
            }
            goto LAB_008320a8;
          }
          goto LAB_00832d24;
        }
        if (iVar10 != 0xf) {
          if (iVar10 < 0x10) {
            if (iVar10 == 0xd) {
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7ed4);
              *(undefined4 *)(param_1 + 0x44) = 0;
              return;
            }
            if (iVar10 < 0xe) {
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7ed8);
              *(undefined4 *)(param_1 + 0x44) = 0;
              return;
            }
LAB_00833858:
            *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7ef0);
            *(undefined4 *)(param_1 + 0x44) = 0;
            return;
          }
          if (iVar10 < 0x15) {
            if (iVar10 < 0x13) {
              if (iVar10 != 0x10) {
                return;
              }
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7ed0);
              *(undefined4 *)(param_1 + 0x44) = 0;
              return;
            }
          }
          else if (iVar10 != 0x18) {
            return;
          }
          goto LAB_008320a8;
        }
      }
LAB_00832d38:
      *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7ee8);
      *(undefined4 *)(param_1 + 0x44) = 0;
      return;
    }
    if (iVar10 < 0x38) {
      if (iVar10 < 0x35) {
        if (iVar10 < 0x28) {
          if (iVar10 < 0x25) {
            if (iVar10 == 0x1d) {
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7ee4);
              *(ulonglong *)(param_1 + 0x140) = *(ulonglong *)(param_1 + 0x140) | 0x8000000000000000
              ;
              *(undefined4 *)(param_1 + 0x44) = 0;
              return;
            }
            if (iVar10 != 0x1e) {
              return;
            }
            *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7ee0);
            *(ulonglong *)(param_1 + 0x140) = *(ulonglong *)(param_1 + 0x140) | 0x8000000000000000;
            *(undefined4 *)(param_1 + 0x44) = 0;
            return;
          }
          goto LAB_00832834;
        }
        if (iVar10 != 0x2f) {
          if (iVar10 < 0x30) {
            if (iVar10 != 0x2e) {
              return;
            }
          }
          else if (iVar10 < 0x32) {
            return;
          }
          goto LAB_008320a8;
        }
      }
LAB_00832d24:
      *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7eec);
      *(undefined4 *)(param_1 + 0x44) = 0;
      return;
    }
    if (iVar10 < 0x42) {
      if (iVar10 < 0x3e) {
        if (iVar10 < 0x3b) {
          return;
        }
        goto LAB_00832834;
      }
    }
    else if ((iVar10 != 0x2cc) && (iVar10 != 0x2ce)) {
      if (iVar10 != 0x43) {
        return;
      }
      goto LAB_00832d24;
    }
LAB_008320a8:
    *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7ef4);
    *(undefined4 *)(param_1 + 0x44) = 0;
    return;
  }
  if (0x30 < (int)uVar9) {
    if (uVar9 == 0x34) {
      if (*(int *)(iVar10 + 0x82c) != 0) {
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f64);
        *(undefined4 *)(param_1 + 0x44) = 0;
        return;
      }
      if ((*(int *)(iVar10 + 0x828) != 1) && (*(int *)(iVar10 + 0x828) != 0x34)) {
        uVar6 = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f64);
        *(undefined4 *)(param_1 + 0x44) = 0;
        *(undefined4 *)(param_1 + 0x40) = uVar6;
        return;
      }
      cVar12 = _opd_FUN_00333608(*(undefined4 *)(iVar3 + 0x24),0,0);
      if (cVar12 == '\0') {
        return;
      }
      uVar6 = *(undefined4 *)(puVar8 + -0x7f64);
      *(undefined4 *)(param_1 + 0x44) = 0;
      *(undefined4 *)(param_1 + 0x40) = uVar6;
      return;
    }
    if (0x34 < (int)uVar9) {
      if (uVar9 == 0x36) {
        if (*(int *)(iVar10 + 0x82c) != 0) {
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f5c);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        iVar10 = *(int *)(iVar10 + 0x828);
        if (0x34 < iVar10) {
          if (iVar10 < 0x37) {
            return;
          }
          if (iVar10 == 0x37) {
            uVar6 = *(undefined4 *)(PTR_DAT_0121b88c + -0x7e9c);
            *(undefined4 *)(param_1 + 0x44) = 0;
            *(undefined4 *)(param_1 + 0x40) = uVar6;
            return;
          }
        }
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f5c);
        *(undefined4 *)(param_1 + 0x44) = 0;
        return;
      }
      if (0x36 < (int)uVar9) {
        if (*(int *)(iVar10 + 0x82c) != 0) {
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f5c);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        if ((*(int *)(iVar10 + 0x828) != 2) && (*(int *)(iVar10 + 0x828) != 0x37)) {
          uVar6 = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f5c);
          *(undefined4 *)(param_1 + 0x44) = 0;
          *(undefined4 *)(param_1 + 0x40) = uVar6;
          return;
        }
        cVar12 = _opd_FUN_00333608(*(undefined4 *)(iVar3 + 0x24),0,0);
        if (cVar12 == '\0') {
          return;
        }
        uVar6 = *(undefined4 *)(puVar8 + -0x7f5c);
        *(undefined4 *)(param_1 + 0x44) = 0;
        *(undefined4 *)(param_1 + 0x40) = uVar6;
        return;
      }
      if (*(int *)(iVar10 + 0x82c) != 0) {
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f5c);
        *(undefined4 *)(param_1 + 0x44) = 0;
        return;
      }
      if (*(int *)(iVar10 + 0x828) - 0x35U < 2) {
        cVar12 = _opd_FUN_00333608(*(undefined4 *)(iVar3 + 0x24),0,0);
        if (cVar12 == '\0') {
          return;
        }
        uVar6 = *(undefined4 *)(puVar8 + -0x7ea0);
        *(undefined4 *)(param_1 + 0x44) = 0;
        *(undefined4 *)(param_1 + 0x40) = uVar6;
        return;
      }
      uVar6 = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f5c);
      *(undefined4 *)(param_1 + 0x44) = 0;
      *(undefined4 *)(param_1 + 0x40) = uVar6;
      return;
    }
    if (uVar9 == 0x32) {
      if (*(int *)(iVar10 + 0x82c) != 0) {
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f64);
        *(undefined4 *)(param_1 + 0x44) = 0;
        return;
      }
      if (*(int *)(iVar10 + 0x828) - 0x32U < 2) {
        cVar12 = _opd_FUN_00333608(*(undefined4 *)(iVar3 + 0x24),0,0);
        if (cVar12 == '\0') {
          return;
        }
        uVar6 = *(undefined4 *)(puVar8 + -0x7ea8);
        *(undefined4 *)(param_1 + 0x44) = 0;
        *(undefined4 *)(param_1 + 0x40) = uVar6;
        return;
      }
      uVar6 = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f64);
      *(undefined4 *)(param_1 + 0x44) = 0;
      *(undefined4 *)(param_1 + 0x40) = uVar6;
      return;
    }
    if (0x32 < (int)uVar9) {
      if (*(int *)(iVar10 + 0x82c) != 0) {
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f64);
        *(undefined4 *)(param_1 + 0x44) = 0;
        return;
      }
      iVar10 = *(int *)(iVar10 + 0x828);
      if (0x31 < iVar10) {
        if (iVar10 < 0x34) {
          return;
        }
        if (iVar10 == 0x34) {
          uVar6 = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ea4);
          *(undefined4 *)(param_1 + 0x44) = 0;
          *(undefined4 *)(param_1 + 0x40) = uVar6;
          return;
        }
      }
      *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f64);
      *(undefined4 *)(param_1 + 0x44) = 0;
      return;
    }
    goto LAB_0083285c;
  }
  if (uVar9 == 0x2c) {
    iVar10 = _opd_FUN_00333360(*(undefined4 *)(iVar3 + 0x24),0,0);
    if (iVar10 < 0x6e) {
      return;
    }
    uVar6 = *(undefined4 *)(puVar8 + -0x7f30);
    *(undefined4 *)(*(int *)(iVar3 + 0x28) + 0x3ef8) = 0xc3fa0000;
    *(undefined4 *)(param_1 + 0x40) = uVar6;
    *(undefined4 *)(param_1 + 0x44) = 0;
    return;
  }
  if ((int)uVar9 < 0x2d) {
    if (uVar9 != 0x27) {
      if (0x29 < (int)uVar9) {
        return;
      }
      goto LAB_00831d58;
    }
    iVar10 = *(int *)(iVar10 + 0x828);
    if (iVar10 != 0x24) {
      if (iVar10 < 0x25) {
        if (0xd < iVar10) {
          if (iVar10 == 0x1b) {
LAB_00833e90:
            *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f80);
            *(undefined4 *)(param_1 + 0x44) = 0;
            return;
          }
          if (iVar10 < 0x1c) {
            if (iVar10 == 0xf) goto LAB_00832e70;
            if (0xe < iVar10) {
              if (iVar10 != 0x10) {
                return;
              }
              goto LAB_00833e58;
            }
          }
          else {
            if (iVar10 == 0x20) {
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ec0);
              *(ulonglong *)(param_1 + 0x140) = *(ulonglong *)(param_1 + 0x140) | 0x8000000000000000
              ;
              *(undefined4 *)(param_1 + 0x44) = 0;
              return;
            }
            if (iVar10 != 0x23) {
              if (iVar10 != 0x1f) {
                return;
              }
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ec4);
              *(ulonglong *)(param_1 + 0x140) = *(ulonglong *)(param_1 + 0x140) | 0x8000000000000000
              ;
              *(undefined4 *)(param_1 + 0x44) = 0;
              return;
            }
          }
          goto LAB_00833eb8;
        }
        if (0xb < iVar10) {
LAB_00833e58:
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ec4);
          *(ulonglong *)(param_1 + 0x140) = *(ulonglong *)(param_1 + 0x140) | 0x8000000000000000;
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        if (iVar10 < 5) {
          if (2 < iVar10) goto LAB_00833e58;
          if (iVar10 == 1) goto LAB_00833eb8;
          if (iVar10 != 2) {
            return;
          }
        }
        else {
          if (iVar10 == 10) goto LAB_00833eb8;
          if (iVar10 != 0xb) {
            return;
          }
        }
      }
      else if (iVar10 < 0x38) {
        if (iVar10 < 0x35) {
          if (iVar10 != 0x2e) {
            if (iVar10 < 0x2f) {
              if (iVar10 == 0x25) {
                *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ebc);
                *(undefined4 *)(param_1 + 0x44) = 0;
                return;
              }
              if (iVar10 != 0x26) {
                return;
              }
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7eb8);
              *(undefined4 *)(param_1 + 0x44) = 0;
              return;
            }
            if (iVar10 == 0x2f) goto LAB_00832e70;
            if (iVar10 < 0x32) {
              return;
            }
          }
LAB_00833eb8:
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ecc);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
      }
      else {
        if (iVar10 < 0x3e) {
          if (0x3a < iVar10) goto LAB_00833e90;
          goto LAB_00833e58;
        }
        if (iVar10 != 0x43) {
          if (iVar10 < 0x44) {
            if (0x41 < iVar10) {
              return;
            }
          }
          else if (iVar10 != 0x2ce) {
            return;
          }
          goto LAB_00833eb8;
        }
      }
    }
LAB_00832e70:
    *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7ec8);
    *(undefined4 *)(param_1 + 0x44) = 0;
    return;
  }
  if (uVar9 != 0x2e) {
    if ((int)uVar9 < 0x2f) {
      iVar10 = _opd_FUN_00333360(*(undefined4 *)(iVar3 + 0x24),0,0);
      if (iVar10 < 0x5a) {
        return;
      }
      uVar6 = *(undefined4 *)(puVar8 + -0x7f6c);
      *(undefined4 *)(*(int *)(iVar3 + 0x28) + 0x3ef8) = 0xc3fa0000;
      *(undefined4 *)(param_1 + 0x40) = uVar6;
      *(undefined4 *)(param_1 + 0x44) = 0;
      return;
    }
LAB_00832a90:
    if ((*(int *)(iVar10 + 0x8c) == 0) && (*(int *)(iVar10 + 0x828) == 0x42)) {
      uVar6 = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f68);
      *(undefined4 *)(param_1 + 0x44) = 0;
      *(undefined4 *)(param_1 + 0x40) = uVar6;
    }
    if ((*(int *)(iVar10 + 0x8c) == 3) && (*(int *)(iVar10 + 0x828) - 0x35U < 2)) {
      *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f58);
      *(undefined4 *)(param_1 + 0x44) = 0;
      return;
    }
    iVar11 = *(int *)(iVar10 + 0x828);
    if (*(int *)(iVar10 + 0x90) == iVar11) {
      return;
    }
    if (iVar11 < 0x16) {
      if (iVar11 < 0x13) {
        if (iVar11 == 0xb) {
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f6c);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
        if (iVar11 < 0xc) {
          if (iVar11 < 6) {
            if (2 < iVar11) goto LAB_00833f5c;
            if (iVar11 != 1) {
              if (iVar11 != 2) {
                return;
              }
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f5c);
              *(undefined4 *)(param_1 + 0x44) = 0;
              return;
            }
          }
          else if (iVar11 != 10) {
            return;
          }
        }
        else if (iVar11 != 0xe) {
          if (0xd < iVar11) {
            if (iVar11 == 0xf) {
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f0c);
              *(undefined4 *)(param_1 + 0x44) = 0;
              return;
            }
            if (iVar11 != 0x10) {
              return;
            }
          }
LAB_00833f5c:
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f00);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
      }
    }
    else if (iVar11 < 0x3b) {
      if (0x37 < iVar11) goto LAB_00833f5c;
      if (iVar11 == 0x2f) {
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f70);
        *(undefined4 *)(param_1 + 0x44) = 0;
        return;
      }
      if (iVar11 < 0x30) {
        if (iVar11 != 0x18) {
          if (iVar11 != 0x2e) {
            return;
          }
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f34);
          *(undefined4 *)(param_1 + 0x44) = 0;
          return;
        }
      }
      else if (2 < iVar11 - 0x32U) {
        return;
      }
    }
    else {
      if (iVar11 == 0x43) {
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7efc);
        *(undefined4 *)(param_1 + 0x44) = 0;
        return;
      }
      if (iVar11 < 0x44) {
        if (3 < iVar11 - 0x3eU) {
          return;
        }
      }
      else if ((iVar11 != 0x2cc) && (iVar11 != 0x2ce)) {
        return;
      }
    }
    *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(puVar8 + -0x7f64);
    *(undefined4 *)(param_1 + 0x44) = 0;
    return;
  }
LAB_00831e40:
  iVar11 = *(int *)(iVar10 + 0x8c);
  if (iVar11 == 3) {
    if (*(int *)(iVar10 + 0x828) - 0x32U < 2) {
      *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f60);
      *(undefined4 *)(param_1 + 0x44) = 0;
      return;
    }
  }
  else if ((iVar11 == 5) && (*(int *)(iVar10 + 0x828) == 0x3e)) {
    *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f48);
    *(undefined4 *)(param_1 + 0x44) = 0;
    return;
  }
  uVar4 = *(uint *)(iVar10 + 0x828);
  if (uVar9 == uVar4) goto LAB_00831e9c;
  if (uVar4 == 0x18) {
LAB_008337d0:
    *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f2c);
    *(undefined4 *)(param_1 + 0x44) = 0;
    goto LAB_00831e9c;
  }
  if ((int)uVar4 < 0x19) {
    if ((int)uVar4 < 0xe) {
      if ((int)uVar4 < 0xb) {
        if (5 < (int)uVar4) {
          if (uVar4 == 10) {
            *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f30);
            *(undefined4 *)(param_1 + 0x44) = 0;
          }
          goto LAB_00831e9c;
        }
        if ((int)uVar4 < 2) {
          if (uVar4 == 1) {
            *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f64);
            *(undefined4 *)(param_1 + 0x44) = 0;
          }
          goto LAB_00831e9c;
        }
      }
    }
    else {
      if (0x10 < (int)uVar4) {
        if (uVar4 - 0x13 < 3) {
          *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f90);
          *(undefined4 *)(param_1 + 0x44) = 0;
        }
        goto LAB_00831e9c;
      }
      if ((int)uVar4 < 0xf) goto LAB_008337d0;
    }
  }
  else {
    if (0x40 < (int)uVar4) {
      if (uVar4 != 0x2c8) {
        if ((int)uVar4 < 0x2c9) {
          if (uVar4 != 0x43) {
            if (uVar4 == 0x44) {
              *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f28);
              *(undefined4 *)(param_1 + 0x44) = 0;
            }
            goto LAB_00831e9c;
          }
          goto LAB_00831e8c;
        }
        if (uVar4 == 0x2cc) goto LAB_008337d0;
        if (uVar4 != 0x2dd) goto LAB_00831e9c;
      }
      *(undefined4 *)(param_1 + 0x44) = 0;
      *(undefined4 *)(param_1 + 0x40) = 0xc5;
      goto LAB_00831e9c;
    }
    if (0x3e < (int)uVar4) {
      if (iVar11 == 0) {
        uVar6 = *(undefined4 *)(PTR_DAT_0121b88c + -0x7fb0);
        *(undefined4 *)(param_1 + 0x44) = 0;
        *(undefined4 *)(param_1 + 0x40) = uVar6;
      }
      else {
        *(undefined4 *)(param_1 + 0x44) = 0;
        *(undefined4 *)(param_1 + 0x40) = 0xd1;
      }
      goto LAB_00831e9c;
    }
    if (uVar4 == 0x2f) {
      *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f70);
      *(undefined4 *)(param_1 + 0x44) = 0;
      goto LAB_00831e9c;
    }
    if ((int)uVar4 < 0x30) {
      if (uVar4 == 0x2e) {
        *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f34);
        *(undefined4 *)(param_1 + 0x44) = 0;
      }
      goto LAB_00831e9c;
    }
    if (5 < uVar4 - 0x35) goto LAB_00831e9c;
  }
LAB_00831e8c:
  *(undefined4 *)(param_1 + 0x40) = *(undefined4 *)(PTR_DAT_0121b88c + -0x7f5c);
  *(undefined4 *)(param_1 + 0x44) = 0;
LAB_00831e9c:
  iVar11 = *(int *)(iVar10 + 0x8c);
  if (iVar11 != 6) {
    if (iVar11 != 9) {
      if (iVar11 != 4) {
        return;
      }
      if (*(int *)(iVar10 + 0x824) != 0x2c8) {
        return;
      }
      *(undefined4 *)(param_1 + 0x44) = 0;
      *(undefined4 *)(param_1 + 0x40) = 0xc5;
      return;
    }
    if ((*(int *)(iVar10 + 0x824) != 0x2cc) && (*(int *)(iVar10 + 0x824) != 0x2dd)) {
      return;
    }
    *(undefined4 *)(param_1 + 0x44) = 0;
    *(undefined4 *)(param_1 + 0x40) = 0xc5;
  }
  if (*(int *)(iVar10 + 0x824) != 0x2dd) {
    return;
  }
  *(undefined4 *)(param_1 + 0x44) = 0;
  *(undefined4 *)(param_1 + 0x40) = 0xc5;
  return;
}


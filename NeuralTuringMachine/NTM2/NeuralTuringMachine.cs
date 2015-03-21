﻿using System;
using NTM2.Controller;
using NTM2.Learning;
using NTM2.Memory;

namespace NTM2
{
    public class NeuralTuringMachine
    {
        internal readonly FeedForwardController Controller;
        internal readonly MemoryState MemoryState;

        public NeuralTuringMachine(int inputSize, int outputSize, int controllerSize, int headCount, int memoryColumnsN, int memoryRowsM)
        {
            MemoryState = new MemoryState(new NTMMemory(memoryColumnsN, memoryRowsM, headCount));
            MemoryState.DoInitialReading();
            Controller = new FeedForwardController(controllerSize, inputSize, outputSize, headCount, memoryRowsM);
        }

        private NeuralTuringMachine(FeedForwardController controller, MemoryState memoryState)
        {
            MemoryState = memoryState;
            Controller = controller;
        }
        
        internal NeuralTuringMachine Clone()
        {
            //TODO refactor ... clone also memory state
            return new NeuralTuringMachine(Controller.Clone(), MemoryState);
        }

        internal void ForwardPropagation(MemoryState oldMemoryState, double[] input)
        {
            Controller.ForwardPropagation(input, oldMemoryState.ReadData);

            for (int i = 0; i < MemoryState.Memory.HeadCount; i++)
            {
                Controller.OutputLayer.HeadsNeurons[i].OldHeadSettings = oldMemoryState.HeadSettings[i];
            }
        }
        
        //TODO replace with weight initialization in constructor
        public void UpdateWeights(Action<Unit> updateAction)
        {
            MemoryState.Memory.UpdateWeights(updateAction);
            Controller.UpdateWeights(updateAction);
        }
        
        internal void UpdateWeights(IWeightUpdater weightUpdater)
        {
            MemoryState.Memory.UpdateWeights(weightUpdater);
            Controller.UpdateWeights(weightUpdater);
        }

        public double[] GetOutput()
        {
            return Controller.GetOutput();
        }
    }
}
